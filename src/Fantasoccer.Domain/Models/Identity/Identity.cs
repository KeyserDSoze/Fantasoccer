using System.Collections.Generic;

namespace Fantasoccer
{
    public record Identity(string Username, string Password, string Name, string Surname, string Email, bool IsActive, bool IsAdmin, bool IsSuperAdmin, List<TeamByGroup> Groups, Settings Settings);
}