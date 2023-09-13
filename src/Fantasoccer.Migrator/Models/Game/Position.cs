using System.Text.Json.Serialization;
using static FantacalcioV4.OldField;

namespace FantacalcioV4
{
    public record Position
    {
        public Position() { }
        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }
        public FantaSoccerRole status = FantaSoccerRole.All;
        public Role Role { get; set; }
        public int Year { get; set; }
        public int SerieADay { get; set; }
        public bool IsInHome { get; set; }
        [JsonIgnore]
        public FantaSoccerRole Status
        {
            get
            {
                if (status == FantaSoccerRole.All)
                {
                    if (Year <= 1)
                    {
                        status = OldField.GetStatus(this, Role);
                    }
                    else if (Year == 2 || Year == 3 || Year == 4 || (Year == 5 && SerieADay < 19))
                    {
                        status = OldField2.GetStatus(this, Role);
                    }
                    else if (Year == 5 && SerieADay >= 19)
                    {
                        status = MinorOldField.GetStatus(IsInHome, this);
                    }
                    else
                    {
                        if (Field.InField.IsValid(this.X))
                        {
                            if (Field.GoalKeeperLine.IsValid(this.Y))
                                status = FantaSoccerRole.GoalKeeper;
                            else if (Field.DefensorLine.IsValid(this.Y))
                                status = FantaSoccerRole.Defensor;
                            else if (Field.MidfielderLine.IsValid(this.Y))
                                status = FantaSoccerRole.Midfielder;
                            else if (Field.ForwardLine.IsValid(this.Y))
                                status = FantaSoccerRole.Forward;
                        }
                        else if (Field.FirstBackup.IsValid(this.X))
                        {
                            if (Field.GoalKeeperLine.IsValid(this.Y))
                                status = FantaSoccerRole.BackupGoalKeeper;
                            else if (Field.DefensorBackup.IsValid(this.Y))
                                status = FantaSoccerRole.FirstBackupDefensor;
                            else if (Field.MidfielderBackup.IsValid(this.Y))
                                status = FantaSoccerRole.FirstBackupMidfielder;
                            else if (Field.ForwardBackup.IsValid(this.Y))
                                status = FantaSoccerRole.FirstBackupForward;
                        }
                        else if (Field.SecondBackup.IsValid(this.X))
                        {
                            if (Field.DefensorBackup.IsValid(this.Y))
                                status = FantaSoccerRole.SecondBackupDefensor;
                            else if (Field.MidfielderBackup.IsValid(this.Y))
                                status = FantaSoccerRole.SecondBackupMidfielder;
                            else if (Field.ForwardBackup.IsValid(this.Y))
                                status = FantaSoccerRole.SecondBackupForward;
                        }
                        else if (Field.TribuneX.IsValid(this.X) && Field.TribuneY.IsValid(this.Y))
                            status = FantaSoccerRole.Tribune;
                        else
                            status = FantaSoccerRole.Invasion;
                        if (status == FantaSoccerRole.All)
                            status = FantaSoccerRole.Tribune;
                    }
                }
                return status;
            }
        }

        public int X { get; set; }
        public int Y { get; set; }
    }
}
