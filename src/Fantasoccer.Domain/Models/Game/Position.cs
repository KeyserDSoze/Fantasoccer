using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record Position(int X, int Y)
    {
        private FantaSoccerRole status = FantaSoccerRole.All;
        [JsonIgnore]
        public FantaSoccerRole Status
        {
            get
            {
                if (status == FantaSoccerRole.All)
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
                return status;
            }
        }
    }
}