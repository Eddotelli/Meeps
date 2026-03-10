using Microsoft.AspNetCore.Identity;
using Shared.Enums;

namespace API.Infrastructure.Data;

public static class RoleSeeder
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole<int>> roleManager)
    {
        foreach (UserRole role in Enum.GetValues<UserRole>())
        {
            var roleName = role.ToString();
            
            // Check if role already exists by trying to find it
            var existingRole = await roleManager.FindByNameAsync(roleName);
            if (existingRole == null)
            {
                var result = await roleManager.CreateAsync(new IdentityRole<int>(roleName));
                if (!result.Succeeded)
                {
                    // Log error but don't throw - role might have been created by another thread/test
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    Console.WriteLine($"Failed to create role {roleName}: {errors}");
                }
            }
        }
    }
}
