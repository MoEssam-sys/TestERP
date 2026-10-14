namespace One.Domain.DTOs
{
    public sealed record DeleteEntityRequest<TKey>(TKey Id, byte[] RowVersion);
}
