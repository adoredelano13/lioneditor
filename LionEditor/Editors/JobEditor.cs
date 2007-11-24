using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace LionEditor
{
    public partial class JobEditor : UserControl
    {
        public event EventHandler DataChangedEvent;

        private void FireDataChangedEvent()
        {
            if (DataChangedEvent != null)
            {
                DataChangedEvent( this, EventArgs.Empty );
            }
        }

        private LionEditor.JobsAndAbilities.Job m_job;
        public LionEditor.JobsAndAbilities.Job Job
        {
        	get { return m_job; }
        	set { m_job = value; }
        }

        private JobInfo m_Info;
        public JobInfo Info
        {
        	get { return m_Info; }
        	set { m_Info = value; }
        }

        private CheckBox[] actionCheckBoxes;
        private CheckBox[] ActionCheckBoxes
        {
            get
            {
                if( actionCheckBoxes == null )
                {
                    actionCheckBoxes = new CheckBox[16];
                    actionCheckBoxes[0] = action1;
                    actionCheckBoxes[1] = action2;
                    actionCheckBoxes[2] = action3;
                    actionCheckBoxes[3] = action4;
                    actionCheckBoxes[4] = action5;
                    actionCheckBoxes[5] = action6;
                    actionCheckBoxes[6] = action7;
                    actionCheckBoxes[7] = action8;
                    actionCheckBoxes[8] = action9;
                    actionCheckBoxes[9] = action10;
                    actionCheckBoxes[10] = action11;
                    actionCheckBoxes[11] = action12;
                    actionCheckBoxes[12] = action13;
                    actionCheckBoxes[13] = action14;
                    actionCheckBoxes[14] = action15;
                    actionCheckBoxes[15] = action16;
                }

                return actionCheckBoxes;
            }
        }

        private CheckBox[] reactionCheckBoxes;
        private CheckBox[] ReactionCheckBoxes
        {
            get
            {
                if( reactionCheckBoxes == null )
                {
                    reactionCheckBoxes = new CheckBox[3];
                    reactionCheckBoxes[0] = reaction1;
                    reactionCheckBoxes[1] = reaction2;
                    reactionCheckBoxes[2] = reaction3;
                }

                return reactionCheckBoxes;
            }
        }

        private CheckBox[] supportCheckBoxes;
        private CheckBox[] SupportCheckBoxes
        {
            get
            {
                if( supportCheckBoxes == null )
                {
                    supportCheckBoxes = new CheckBox[4];
                    supportCheckBoxes[0] = support1;
                    supportCheckBoxes[1] = support2;
                    supportCheckBoxes[2] = support3;
                    supportCheckBoxes[3] = support4;
                }

                return supportCheckBoxes;
            }
        }

        private CheckBox[] movementCheckBoxes;
        private CheckBox[] MovementCheckBoxes
        {
            get
            {
                if( movementCheckBoxes == null )
                {
                    movementCheckBoxes = new CheckBox[2];
                    movementCheckBoxes[0] = movement1;
                    movementCheckBoxes[1] = movement2;
                }

                return movementCheckBoxes;
            }
        }

        bool ignoreChanges = false;

        public void Update( LionEditor.JobsAndAbilities.Job job, JobInfo info )
        {
            this.SuspendLayout();
            ignoreChanges = true;

            this.Info = info;
            this.Job = job;

            int i = 0;
            for( i = 0; i < info.action.Length; i++ )
            {
                ActionCheckBoxes[i].Enabled = true;
                ActionCheckBoxes[i].Visible = true;
                ActionCheckBoxes[i].Text = info.action[i].Name;
                if( i < 8 )
                {
                    ActionCheckBoxes[i].Checked = job.actions1[i % 8];
                }
                else
                {
                    ActionCheckBoxes[i].Checked = job.actions2[i % 8];
                }
            }
            for( ; i < ActionCheckBoxes.Length; i++ )
            {
                ActionCheckBoxes[i].Enabled = false;
                ActionCheckBoxes[i].Visible = false;
            }

            int numReactions = info.reaction.Length;
            int numSupports = info.support.Length;
            int numMovements = info.movement.Length;

            int j = 0;
            for( j = 0; j < info.reaction.Length; j++ )
            {
                ReactionCheckBoxes[j].Enabled = true;
                ReactionCheckBoxes[j].Visible = true;
                ReactionCheckBoxes[j].Text = info.reaction[j].Name;
                ReactionCheckBoxes[j].Checked = job.theRest[j];
            }
            for( i = j; i < ReactionCheckBoxes.Length; i++ )
            {
                ReactionCheckBoxes[i].Enabled = false;
                ReactionCheckBoxes[i].Visible = false;
            }

            for( j = 0; j < numSupports; j++ )
            {
                SupportCheckBoxes[j].Enabled = true;
                SupportCheckBoxes[j].Visible = true;
                SupportCheckBoxes[j].Text = info.support[j].Name;
                SupportCheckBoxes[j].Checked = job.theRest[j + numReactions];
            }
            for( i = j; i < SupportCheckBoxes.Length; i++ )
            {
                SupportCheckBoxes[i].Enabled = false;
                SupportCheckBoxes[i].Visible = false;
            }

            for( j = 0; j < numMovements; j++ )
            {
                MovementCheckBoxes[j].Enabled = true;
                MovementCheckBoxes[j].Visible = true;
                MovementCheckBoxes[j].Text = info.movement[j].Name;
                MovementCheckBoxes[j].Checked = job.theRest[j + numReactions + numSupports];
            }

            for( i = j; i < MovementCheckBoxes.Length; i++ )
            {
                MovementCheckBoxes[i].Enabled = false;
                MovementCheckBoxes[i].Visible = false;
            }

            jpSpinner.Value = job.JP;
            totalSpinner.Value = job.TotalJP;

            ignoreChanges = false;
            this.ResumeLayout();
        }

        public JobEditor()
        {
            InitializeComponent();
            foreach( CheckBox cb in ActionCheckBoxes )
            {
                cb.CheckedChanged += cb_CheckedChanged;
            }
            foreach( CheckBox cb in ReactionCheckBoxes )
            {
                cb.CheckedChanged += cb_CheckedChanged;
            }
            foreach( CheckBox cb in MovementCheckBoxes )
            {
                cb.CheckedChanged += cb_CheckedChanged;
            }
            foreach( CheckBox cb in SupportCheckBoxes )
            {
                cb.CheckedChanged += cb_CheckedChanged;
            }

            jpSpinner.ValueChanged += spinnerValueChanged;
            totalSpinner.ValueChanged += spinnerValueChanged;
        }

        void spinnerValueChanged( object sender, EventArgs e )
        {
            if( !ignoreChanges )
            {
                Job.JP = (ushort)jpSpinner.Value;
                Job.TotalJP = (ushort)totalSpinner.Value;

                FireDataChangedEvent();
            }
        }

        void cb_CheckedChanged( object sender, EventArgs e )
        {
            CheckBox cb = sender as CheckBox;

            if( !ignoreChanges )
            {
                string s = cb.Name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                int num = Convert.ToInt32( cb.Name.Substring( s.Length ) );

                switch( s )
                {
                    case "action":
                        if( num <= 8 )
                        {
                            Job.actions1[num - 1] = cb.Checked;
                        }
                        else
                        {
                            Job.actions2[num - 9] = cb.Checked;
                        }
                        break;
                    case "reaction":
                        Job.theRest[num - 1] = cb.Checked;
                        break;
                    case "support":
                        Job.theRest[num + Info.reaction.Length - 1] = cb.Checked;
                        break;
                    case "movement":
                        Job.theRest[num + Info.reaction.Length + Info.support.Length - 1] = cb.Checked;
                        break;
                }

                FireDataChangedEvent();
            }
        }


    }
}
