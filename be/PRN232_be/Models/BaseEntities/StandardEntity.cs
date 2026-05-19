namespace PRN232_be.Models.BaseEntities
{
    public abstract class StandardEntity<TKey> : AuditableEntity<TKey>
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? TextSearch { get; set; }
    }
}
