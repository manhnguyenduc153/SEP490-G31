namespace sep490_be.Models.BaseEntities
{
    public abstract class BaseEntity<TKey>
    {
        public TKey Id { get; set; } = default!;
    }
}

