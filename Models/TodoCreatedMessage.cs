namespace DotNetCoreSqlDb.Models
{
    public record TodoCreatedMessage(int TodoId, string Description, DateTimeOffset CreatedAt);
}
