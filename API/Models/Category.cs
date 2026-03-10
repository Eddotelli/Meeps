using Shared.Enums;

namespace API.Models;

public class Category
{
    public int Id { get; set; }
    public CategoryType Type { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<UserCategory> UserCategories { get; set; } = new List<UserCategory>();
}
