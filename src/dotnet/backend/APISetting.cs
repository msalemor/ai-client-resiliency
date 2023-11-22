public class APISetting()
{
    public DateTime Timeout { get; set; } = DateTime.UtcNow;
    public bool Available { get; set; } = true;
    public APIRule Rule { get; set; } = APIRule.None;
};
