namespace One.Application.Services.Mapping
{
    public interface IDynamicMappingService
    {
        TEntity MapCreate<TEntity, TRequest>(TRequest source) where TEntity : new();

        TEntity MapUpdate<TEntity, TRequest>(TRequest source) where TEntity : new();

        TEntity MapDelete<TEntity, TRequest>(TRequest source) where TEntity : new();

        TEntity MapActive<TEntity, TRequest>(TRequest source) where TEntity : new();
    }
}