namespace FantacalcioV4
{
    public static class Field
    {
        public const int Span = 90;
        public const int Max = 410;
        public static readonly FieldRange GoalKeeperLine = new(0, 90);
        public static readonly FieldRange DefensorLine = new(91, 210);
        public static readonly FieldRange MidfielderLine = new(211, 320);
        public static readonly FieldRange ForwardLine = new(321, 410);
        public static readonly FieldRange InField = new(0, 500);
        public static readonly FieldRange FirstBackup = new(620, 700);
        public static readonly FieldRange SecondBackup = new(701, 800);
        public static readonly FieldRange GoalKeeperBackup = new(0, 40);
        public static readonly FieldRange DefensorBackup = new(125, 165);
        public static readonly FieldRange MidfielderBackup = new(250, 290);
        public static readonly FieldRange ForwardBackup = new(375, 415);
        public static readonly FieldRange TribuneY = new(1000, 1600);
        public static readonly FieldRange TribuneX = new(0, 1000);
        public static readonly FieldRange Invasion = new(416, 1000);
    }
    public static class MinorOldField
    {
        public static FantaSoccerRole GetStatus(bool isTeamA, Position position)
        {
            if (position.X >= 0 && position.X <= 500)
            {
                if ((isTeamA && position.Y >= 0 && position.Y <= 90) || (!isTeamA && position.Y >= 831 && position.Y <= 920))
                {
                    return FantaSoccerRole.GoalKeeper;
                }
                else if ((isTeamA && position.Y > 90 && position.Y <= 210) || (!isTeamA && position.Y > 710 && position.Y <= 830))
                {
                    return FantaSoccerRole.Defensor;
                }
                else if ((isTeamA && position.Y > 210 && position.Y <= 320) || (!isTeamA && position.Y > 600 && position.Y <= 710))
                {
                    return FantaSoccerRole.Midfielder;
                }
                else if ((isTeamA && position.Y > 320 && position.Y <= 410) || (!isTeamA && position.Y >= 500 && position.Y <= 600))
                {
                    return FantaSoccerRole.Forward;
                }
            }
            if ((position.X >= 620 && position.X <= 700) || (position.X >= 700 && position.X <= 800))
            {
                if ((isTeamA && position.Y >= 0 && position.Y <= 40) || (!isTeamA && position.Y >= 875 && position.Y <= 915))
                {
                    return FantaSoccerRole.BackupGoalKeeper;
                }
                else if ((isTeamA && position.Y >= 125 && position.Y <= 165) || (!isTeamA && position.Y >= 750 && position.Y <= 790))
                {
                    if (position.X >= 620 && position.X <= 700)
                    {
                        return FantaSoccerRole.FirstBackupDefensor;
                    }
                    else
                    {
                        return FantaSoccerRole.SecondBackupDefensor;
                    }
                }
                else if ((isTeamA && position.Y >= 250 && position.Y <= 290) || (!isTeamA && position.Y >= 625 && position.Y <= 665))
                {
                    if (position.X >= 620 && position.X <= 700)
                    {
                        return FantaSoccerRole.FirstBackupMidfielder;
                    }
                    else
                    {
                        return FantaSoccerRole.SecondBackupMidfielder;
                    }
                }
                else if ((isTeamA && position.Y > 375 && position.Y <= 415) || (!isTeamA && position.Y > 500 && position.Y <= 540))
                {
                    if (position.X >= 620 && position.X <= 700)
                    {
                        return FantaSoccerRole.FirstBackupForward;
                    }
                    else
                    {
                        return FantaSoccerRole.SecondBackupForward;
                    }
                }
            }
            return FantaSoccerRole.Tribune;
        }
    }
    public static class OldField
    {
        public static FantaSoccerRole GetStatus(Position position, Role role)
        {
            bool isTeamA = true;
            if (isTeamA)
            {
                if (position.Y >= 1000 || position.X >= 1000)
                    return FantaSoccerRole.Tribune;
                if (position.Y <= 600 && position.X < 355)
                {
                    switch (role)
                    {
                        case Role.GoalKeeper:
                            return FantaSoccerRole.GoalKeeper;
                        case Role.Defensor:
                            return FantaSoccerRole.Defensor;
                        case Role.Midfielder:
                            return FantaSoccerRole.Midfielder;
                        case Role.Forward:
                            return FantaSoccerRole.Forward;
                    }
                }
                else if (position.X < 355)
                {
                    switch (role)
                    {
                        case Role.GoalKeeper:
                            return FantaSoccerRole.BackupGoalKeeper;
                        case Role.Defensor:
                            if (position.Y <= 650)
                                return FantaSoccerRole.FirstBackupDefensor;
                            else
                                return FantaSoccerRole.SecondBackupDefensor;
                        case Role.Midfielder:
                            if (position.Y <= 650)
                                return FantaSoccerRole.FirstBackupMidfielder;
                            else
                                return FantaSoccerRole.SecondBackupMidfielder;
                        case Role.Forward:
                            if (position.Y <= 650)
                                return FantaSoccerRole.FirstBackupForward;
                            else
                                return FantaSoccerRole.SecondBackupForward;
                    }
                }
            }
            return FantaSoccerRole.Tribune;
        }
        public static class OldField2
        {
            public static FantaSoccerRole GetStatus(Position position, Role role)
            {
                bool isTeamA = true;
                if (isTeamA)
                {
                    if (position.Y >= 850 || position.X >= 850)
                        return FantaSoccerRole.Tribune;
                    if (position.X == 365)
                    {
                        switch (role)
                        {
                            case Role.GoalKeeper:
                                return FantaSoccerRole.BackupGoalKeeper;
                            case Role.Defensor:
                                if (position.Y <= 150)
                                    return FantaSoccerRole.FirstBackupDefensor;
                                else
                                    return FantaSoccerRole.SecondBackupDefensor;
                            case Role.Midfielder:
                                if (position.Y <= 280)
                                    return FantaSoccerRole.FirstBackupMidfielder;
                                else
                                    return FantaSoccerRole.SecondBackupMidfielder;
                            case Role.Forward:
                                if (position.Y <= 400)
                                    return FantaSoccerRole.FirstBackupForward;
                                else
                                    return FantaSoccerRole.SecondBackupForward;
                        }
                    }
                    else if (position.Y <= 800)
                    {
                        switch (role)
                        {
                            case Role.GoalKeeper:
                                return FantaSoccerRole.GoalKeeper;
                            case Role.Defensor:
                                return FantaSoccerRole.Defensor;
                            case Role.Midfielder:
                                return FantaSoccerRole.Midfielder;
                            case Role.Forward:
                                return FantaSoccerRole.Forward;
                        }
                    }
                }
                return FantaSoccerRole.Tribune;
            }
        }
    }
}
