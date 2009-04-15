﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using PatcherLib.Datatypes;
using PatcherLib;
using PatcherLib.Utilities;

namespace FFTPatcher.SpriteEditor
{
    public class AllSprites
    {
        private IList<Sprite> sprites;
        private AllSpriteAttributes attrs;
        private SpriteFileLocations locs;

        public const int NumSprites = 154;
        const long defaultIsoLength = 541315152;
        const long expandedIsoLength = 0x20F18D00;
        const long defaultSectorCount = 230151;
        const long expandedSectorCount = 0x20F18D00/2352;


        public Sprite this[int i]
        {
            get { return sprites[i]; }
        }

        public static AllSprites FromPsxIso(Stream iso)
        {
            if (!DetectExpansionOfPsxIso(iso))
            {
                ExpandPsxIso(iso);
            }
            return new AllSprites(AllSpriteAttributes.FromPsxIso(iso), SpriteFileLocations.FromPsxIso(iso));
        }

        struct Time
        {
            private byte min;
            private byte sec;
            private byte f;
            public byte Minutes { get { return min; } }
            public byte Seconds { get { return sec; } }
            public byte Frames { get { return f; } }
            public Time(byte m, byte s, byte f) 
            {
                min = m;
                sec = s;
                this.f = f;
            }

            public Time AddFrame()
            {
                byte newF = (byte)(f + 1);
                byte newS = sec;
                byte newMin = min;

                if ( newF == 75 )
                {
                    newF = 0;
                    newS++;
                    if (newS == 60)
                    {
                        newS = 0;
                        newMin++;
                    }
                }
                return new Time(newMin, newS, newF);
            }
            public byte[] ToBCD()
            {
                return new byte[] {
                    (byte)(min/10 * 16 + min%10),
                    (byte)(sec/10 * 16 + sec%10),
                    (byte)(f/10 * 16 + f%10) };
            }
        }

        public static void ExpandPspIso(Stream iso)
        {
            string tempPath = Path.GetTempPath();
            string guid = Path.GetRandomFileName();
            string tempDirPath = Path.Combine(tempPath, guid);
            DirectoryInfo temp = Directory.CreateDirectory(tempDirPath);

            PatcherLib.Iso.PspIso.PspIsoInfo info = PatcherLib.Iso.PspIso.PspIsoInfo.GetPspIsoInfo(iso);
            long fftpackSector = info[PatcherLib.Iso.PspIso.Sectors.PSP_GAME_USRDIR_fftpack_bin];
            iso.Seek(2048 * fftpackSector, SeekOrigin.Begin);

            PatcherLib.Iso.FFTPack.DumpToDirectory(iso, tempDirPath, info.GetFileSize(PatcherLib.Iso.PspIso.Sectors.PSP_GAME_USRDIR_fftpack_bin), null);
            PatcherLib.Iso.PspIso.DecryptISO(iso, info);

            string effectDirPath = Path.Combine(tempDirPath, "EFFECT");
            string[] effectFiles = Directory.GetFiles(effectDirPath, "E???.BIN", SearchOption.TopDirectoryOnly);
            effectFiles.ForEach(f => File.Delete(Path.Combine(effectDirPath, f)));

            string battleDirPath = Path.Combine(tempDirPath, "BATTLE");


            // Read the sector -> fftpack map
            IList<byte> fftpackMap = 
                PatcherLib.Iso.PspIso.GetBlock(
                    iso, 
                    info, 
                    new PatcherLib.Iso.PspIso.KnownPosition(PatcherLib.Iso.PspIso.Sectors.PSP_GAME_SYSDIR_BOOT_BIN, 0x252f34, 0x3e00));

            Dictionary<uint, int> sectorToFftPackMap = new Dictionary<uint, int>();
            Dictionary<int, uint> fftPackToSectorMap = new Dictionary<int, uint>();
            for (int i = 3; i < PatcherLib.Iso.FFTPack.NumFftPackFiles - 1; i++)
            {
                UInt32 sector = fftpackMap.Sub((i-3)*4,(i-3)*4+4-1).ToUInt32();
                sectorToFftPackMap.Add(sector, i);
                fftPackToSectorMap.Add(i, sector);
            }

            const int numPspSp2 = 0x130/8;
            const int numPspSprites = 0x4d0/8+0x58/8;
            byte[][] oldSpriteBytes = new byte[numPspSprites][];

            var locs = SpriteFileLocations.FromPspIso(iso);
            for (int i = 0; i < numPspSprites; i++)
            {
                oldSpriteBytes[i] = new byte[65536];

                PatcherLib.Iso.FFTPack.GetFileFromIso(iso, info, (PatcherLib.Iso.FFTPack.Files)sectorToFftPackMap[locs[i].Sector]).CopyTo(oldSpriteBytes[i], 0);
            }

            byte[] emptyByteArray = new byte[0];
            // Replace old sprites
            for (int i = 78; i <= 213; i++)
            {
                string currentFile = Path.Combine(tempDirPath, PatcherLib.Iso.FFTPack.FFTPackFiles[i]);
                File.Delete(currentFile);
                File.WriteAllBytes(currentFile, emptyByteArray);
            }
            // 234-745
            for (int i = 234; i <= 745; i++)
            {
                File.WriteAllBytes(Path.Combine(tempDirPath, string.Format("unknown/fftpack.{0}", i)), emptyByteArray);
            }
            for (int i = 0; i < numPspSprites; i++)
            {
                File.WriteAllBytes(Path.Combine(tempDirPath, string.Format("unknown/fftpack.{0}", i + 234)), oldSpriteBytes[i]);
                locs[i].Sector = fftPackToSectorMap[i + 234];
                locs[i].Size = 65536;
            }

            List<byte> newSpriteLocations = new List<byte>();
            for (int i = 0; i < 154; i++)
            {
                newSpriteLocations.AddRange(locs[i].Sector.ToBytes());
                newSpriteLocations.AddRange(locs[i].Size.ToBytes());
            }
            newSpriteLocations.AddRange(new byte[32]);
            for (int i = 154; i < numPspSprites; i++)
            {
                newSpriteLocations.AddRange(locs[i].Sector.ToBytes());
                newSpriteLocations.AddRange(locs[i].Size.ToBytes());
            }

            byte[] newSpriteLocationsArray = newSpriteLocations.ToArray();
            string outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            PatcherLib.Iso.FFTPack.MergeDumpedFiles(tempDirPath, outputPath, null);

            using (Stream newFftPack = File.OpenRead(outputPath))
            {
                CopyStream(newFftPack,0,iso,info[PatcherLib.Iso.PspIso.Sectors.PSP_GAME_USRDIR_fftpack_bin]*2048,newFftPack.Length);
                long oldLength = info.GetFileSize(PatcherLib.Iso.PspIso.Sectors.PSP_GAME_USRDIR_fftpack_bin);
                if (newFftPack.Length < oldLength)
                {
                    iso.Write(new byte[oldLength - newFftPack.Length], 0, (int)(oldLength - newFftPack.Length));
                }
            }
            Directory.Delete(tempDirPath, true);
            File.Delete(outputPath);

            PatcherLib.Iso.PspIso.PatchISO(iso, new PatchedByteArray[] { 
                new PatchedByteArray(PatcherLib.Iso.PspIso.Sectors.PSP_GAME_SYSDIR_BOOT_BIN, 0x324824, newSpriteLocationsArray),
                new PatchedByteArray(PatcherLib.Iso.PspIso.Sectors.PSP_GAME_SYSDIR_EBOOT_BIN, 0x324824, newSpriteLocationsArray)});

        }

        private static void CopyStream(Stream source, long sourcePosition, Stream destination, long destinationPosition, long count)
        {
            long copied = 0;
            byte[] buffer = new byte[2048];
            source.Seek(sourcePosition, SeekOrigin.Begin);
            destination.Seek(destinationPosition, SeekOrigin.Begin);

            while(copied < count)
            {
                int bytesCopied = source.Read(buffer, 0, 2048);
                destination.Write(buffer, 0, bytesCopied);
                copied += bytesCopied;
            }
        }

        public static void ExpandPsxIso(Stream iso)
        {
            byte[] expandedBytes = expandedSectorCount.ToBytes();
            byte[] reverseBytes = new byte[4] { expandedBytes[3], expandedBytes[2], expandedBytes[1], expandedBytes[0] };
            PatcherLib.Iso.PsxIso.PatchPsxIso( iso, PatcherLib.Iso.PsxIso.NumberOfSectorsLittleEndian.GetPatchedByteArray( expandedBytes ) );
            PatcherLib.Iso.PsxIso.PatchPsxIso( iso, PatcherLib.Iso.PsxIso.NumberOfSectorsBigEndian.GetPatchedByteArray( reverseBytes ) );
            //PatcherLib.Iso.PsxIso.PatchPsxIso( iso, 
            //    new PatchedByteArray( 
            //        (PatcherLib.Iso.PsxIso.Sectors)22, 
            //        0xDC, 
            //        new byte[] { 0x00, 0x38, 0x00, 0x00, 0x00, 0x00, 0x38, 0x00 } ) );

            // Build directory entry for /DUMMY
            //iso.Seek(0x203E6500, SeekOrigin.Begin);
            //iso.Write(Properties.Resources.PatchedDummyFolder, 0, Properties.Resources.PatchedDummyFolder.Length);

            // Read old sprites
            var locs = SpriteFileLocations.FromPsxIso(iso);
            byte[][] oldSprites = new byte[NumSprites][];
            for (int i = 0; i < NumSprites; i++)
            {
                var loc = locs[i];
                oldSprites[i] = PatcherLib.Iso.PsxIso.ReadFile(iso, (PatcherLib.Iso.PsxIso.Sectors)loc.Sector, 0, (int)loc.Size);
            }

            Set<string> allowedEntries = new Set<string>( new string[] {
                "\0", "\x01",
                "ARUTE.SEQ;1",                "ARUTE.SHP;1",                
                "CYOKO.SEQ;1",                "CYOKO.SHP;1",                
                "EFC_FNT.TIM;1",                "EFF1.SEQ;1",                "EFF1.SHP;1",                "EFF2.SEQ;1",
                "EFF2.SHP;1",                "ENTD1.ENT;1",                "ENTD2.ENT;1",                "ENTD3.ENT;1",
                "ENTD4.ENT;1",                
                
                "KANZEN.SEQ;1",                "KANZEN.SHP;1",
                "MON.SEQ;1",                "MON.SHP;1",
                "OTHER.SEQ;1",                "OTHER.SHP;1",                "OTHER.SPR;1",                "RUKA.SEQ;1",
                "TYPE1.SEQ;1",                "TYPE1.SHP;1",                "TYPE2.SEQ;1",                "TYPE2.SHP;1",
                "TYPE3.SEQ;1",                "TYPE4.SEQ;1",                "WEP.SPR;1",                "WEP1.SEQ;1",
                "WEP1.SHP;1",                "WEP2.SEQ;1",                "WEP2.SHP;1",                "ZODIAC.BIN;1"});
            
            List<PatcherLib.Iso.PsxIso.DirectoryEntry> battleDir = new List<PatcherLib.Iso.PsxIso.DirectoryEntry>(PatcherLib.Iso.PsxIso.DirectoryEntry.GetBattleBinEntries(iso));
            byte[] extBytes = battleDir[2].ExtendedBytes;
            System.Diagnostics.Debug.Assert(battleDir.Sub(2).TrueForAll(ent => PatcherLib.Utilities.Utilities.CompareArrays(extBytes, ent.ExtendedBytes)));
            byte[] midBytes = battleDir[2].MiddleBytes;
            System.Diagnostics.Debug.Assert(battleDir.Sub(2).TrueForAll(ent => PatcherLib.Utilities.Utilities.CompareArrays(midBytes, ent.MiddleBytes)));
            battleDir.RemoveAll(dirent => !allowedEntries.Contains(dirent.Filename));

            // Expand length of ISO
            byte[] anchorBytes = new byte[] { 
                    0x00, 0xFF, 0xFF, 0xFF, 
                    0xFF, 0xFF, 0xFF, 0xFF, 
                    0xFF, 0xFF, 0xFF, 0x00 };
            byte[] sectorBytes = new byte[] {
                0x00, 0x00, 0x08, 0x00,
                0x00, 0x00, 0x08, 0x00 };
            byte[] endOfFileBytes = new byte[] {
                0x00, 0x00, 0x89, 0x00,
                0x00, 0x00, 0x89, 0x00 };
            //byte[] sectorBytes = new byte[8];
            //byte[] endOfFileBytes = new byte[8];
            byte[] emptySector = new byte[2328];
            Time t = new Time(51, 9, 39);
            for (long l = 0x2040B100; l < 0x20F18D00; l += 2352)
            {
                // write 0x00FFFFFF FFFFFFFF FFFFFF00 MM SS FF 02
                  // write 0x00000800 00000800 for sector of file
                  // write 0x00008900 00008900 for last sector of file
                iso.Seek(l, SeekOrigin.Begin);
                iso.Write(anchorBytes,0, anchorBytes.Length);
                iso.Write(t.ToBCD(), 0, 3);
                t = t.AddFrame();
                iso.WriteByte(0x02);
                if ((l - 0x2040B100+2352) % 0x12600 != 0)
                {
                    iso.Write(sectorBytes, 0, 8);
                }
                else
                {
                    iso.Write(endOfFileBytes, 0, 8);
                }
                iso.Write(emptySector, 0, 2328);
            }


            // Copy old sprites to new locations
            List<byte> posBytes = new List<byte>(NumSprites * 8);
            long startSector = 0x2040B100 / 2352;
            for ( int i = 0; i < NumSprites; i++ )
            {
                uint sector = (uint)( startSector + i * 65536 / 2048 );
                byte[] bytes = oldSprites[i];
                byte[] realBytes = new byte[65536];
                bytes.CopyTo( realBytes, 0 );
                PatcherLib.Iso.PsxIso.PatchPsxIso( iso, new PatchedByteArray( (int)sector, 0, realBytes ) );
                posBytes.AddRange( sector.ToBytes() );
                posBytes.AddRange( ( (uint)bytes.Length ).ToBytes() );

                battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(sector, 65536, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                    string.Format("{0:X2}.SPR;1", i), battleDir[2].ExtendedBytes));
            }

            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_ARLI2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "8C.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_BIBU2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "95.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_BOM2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "87.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_BEHI2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "92.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_DEMON2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "98.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_DORA22_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "94.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_HYOU2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "88.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_IRON5_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "99_2.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_IRON4_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "99_3.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_IRON2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "99_4.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_IRON3_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "99_5.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_MINOTA2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "90.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_MOL2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "91.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_TORI2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "8D.SP2;1", battleDir[2].ExtendedBytes));
            battleDir.Add(new PatcherLib.Iso.PsxIso.DirectoryEntry(
                (uint)PatcherLib.Iso.PsxIso.Sectors.BATTLE_URI2_SP2, 32768, DateTime.Now, battleDir[2].GMTOffset, battleDir[2].MiddleBytes,
                "8E.SP2;1", battleDir[2].ExtendedBytes));

            //"ARLI2.SP2;1",  // 0x8c     
                //"BIBU2.SP2;1", // 0x95
                //"BOM2.SP2;1", // 0x87
                //"BEHI2.SP2;1", // 0x92
                //"DEMON2.SP2;1", // 0x98         
                //"DORA22.SP2;1", // 0x94
                //"HYOU2.SP2;1",  // 0x88
                //"IRON5.SP2;1",                
                //"IRON4.SP2;1",                
                //"IRON2.SP2;1",                
                //"IRON3.SP2;1",
                //"MINOTA2.SP2;1", // 0x90         
                //"MOL2.SP2;1",  // 0x91  
                //"TORI2.SP2;1", // 0x8d
                //"UR2.SP2;1", // 0x8e

            battleDir.Sort((a, b) => a.Filename.CompareTo(b.Filename));
            byte[][] dirEntryBytes = new byte[battleDir.Count][];
            for (int i = 0; i < battleDir.Count; i++)
            {
                dirEntryBytes[i] = battleDir[i].ToByteArray();
            }

            List<byte>[] sectors = new List<byte>[6] { new List<byte>(2048), new List<byte>(2048), new List<byte>(2048),
                new List<byte>(2048), new List<byte>(2048), new List<byte>(2048) };
            int currentSector = 0;
            foreach (byte[] entry in dirEntryBytes)
            {
                if (sectors[currentSector].Count + entry.Length > 2048)
                {
                    currentSector++;
                }
                sectors[currentSector].AddRange(entry);
            }
            foreach (List<byte> sec in sectors)
            {
                sec.AddRange(new byte[2048 - sec.Count]);
            }

            for (int i = 0; i < 6; i++)
            {
                PatcherLib.Iso.PsxIso.PatchPsxIso(iso, new PatchedByteArray((PatcherLib.Iso.PsxIso.Sectors)(56436 + i), 0, sectors[i].ToArray()));
            }

            // Update battle.bin
            PatcherLib.Iso.PsxIso.PatchPsxIso(iso, SpriteFileLocations.SpriteLocationsPosition.GetPatchedByteArray(posBytes.ToArray()));
        }

        public static bool DetectExpansionOfPsxIso(Stream iso)
        {
            UInt32 sectors = PatcherLib.Iso.PsxIso.ReadFile(iso, PatcherLib.Iso.PsxIso.NumberOfSectorsLittleEndian).ToUInt32();

            //38 // length of record
            //00 // nothing
            //D6 E9 00 00 00 00 E9 D6 // sector
            //01 92 00 00 00 00 92 01 // size
            //61 // year
            //05 // month
            //10 // day
            //12 // hour
            //15 // minutes
            //1E // seconds
            //24 // GMT offset
            //01 // hidden file
            //00 00 
            //01 00 00 01 
            //09 // name length
            //31 30 4D 2E 53 50 52 3B 31 // name 10M.SPR;1

            //2A 00 2A 00 // owner id
            //08 01 // attributes
            //58 41  // X A
            //00  // file number
            //00  00 00  00 00 // reserved 

            //30 
            //00 
            //90 82 03 00 00 03 82 90 
            //00 00 01 00 00 01 00 00 
            //61 
            //0A 
            //11 
            //12 
            //25 
            //15 
            //24 
            //00 
            //00 00 
            //01 00 00 01 
            //0E 
            //53 50 52 49 54 45 30 30 2E 53 50 52 3B 31 
            //00 

            return iso.Length > defaultIsoLength &&
                iso.Length >= expandedIsoLength &&
                sectors > defaultSectorCount &&
                sectors >= expandedSectorCount &&
                //!SpriteFileLocations.IsoHasDefaultSpriteLocations( iso ) &&
                SpriteFileLocations.IsoHasPatchedSpriteLocations( iso );
        }

        private AllSprites(AllSpriteAttributes attrs, SpriteFileLocations locs)
        {

            sprites = new Sprite[NumSprites];
            for (int i = 0; i < NumSprites; i++)
            {
                sprites[i] = new Sprite(string.Format("{0:X2} - {1}", i, PSXResources.SpriteFiles[i]), attrs[i], locs[i]);
            }
            this.attrs = attrs;
            this.locs = locs;
        }

    }
}
