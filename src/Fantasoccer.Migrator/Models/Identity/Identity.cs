using System.Collections.Generic;

namespace FantacalcioV4
{
    public record Identity(string Username, string Password, string Name, string Surname, string Email, bool IsActive, bool IsAdmin, bool IsSuperAdmin, List<TeamByGroup> Groups, Settings Settings);
}