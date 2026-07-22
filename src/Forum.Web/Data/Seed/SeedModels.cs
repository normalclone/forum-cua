namespace Forum.Web.Data.Seed;

// Mô hình khớp định dạng JSON do workflow sinh ra (đọc case-insensitive).
public class SeedUserFile { public List<SeedUser> Users { get; set; } = new(); }

public class SeedUser
{
    public string DisplayName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Trade { get; set; } = "Khac";
    public string? Bio { get; set; }
    public string? Location { get; set; }
}

public class SeedCategoryFile { public List<SeedTopic> Topics { get; set; } = new(); }

public class SeedTopic
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsQuestion { get; set; }
    public bool Featured { get; set; }
    public bool Pinned { get; set; }
    public List<string> Tags { get; set; } = new();
    public SeedPoll? Poll { get; set; }
    public List<SeedComment> Comments { get; set; } = new();
}

public class SeedPoll
{
    public string Question { get; set; } = "";
    public List<string> Options { get; set; } = new();
}

public class SeedComment
{
    public string Body { get; set; } = "";
    public List<SeedComment> Replies { get; set; } = new();
}
