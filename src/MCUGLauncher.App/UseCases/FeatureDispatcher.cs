using System.Reflection;
using System.Text.Json;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

internal sealed class FeatureDispatcher
{
    readonly CancellationToken token;
    readonly Dictionary<string, IFeatureHandlerAdapter> handlers;

    public FeatureDispatcher(LauncherContext context)
    {
        token = context.Token;
        var operations = EnumerateInterfaces(typeof(ILauncherService))
            .SelectMany(type => type.GetMethods())
            .Where(method => method.IsDefined(typeof(FeatureOperationAttribute)))
            .GroupBy(method => method.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var operation in operations.Values)
        {
            if (operation.IsAbstract
                || !operation.ReturnType.IsGenericType
                || operation.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
                throw new InvalidOperationException(
                    $"Feature operation '{operation.Name}' must be a default interface method returning Task<TResponse>.");
        }

        handlers = [];
        foreach (var type in typeof(LauncherApplication).Assembly.GetTypes())
        {
            var registration = type.GetCustomAttribute<FeatureHandlerAttribute>();
            if (registration is null)
                continue;
            if (!operations.TryGetValue(registration.Operation, out var operation))
                throw new InvalidOperationException(
                    $"Feature handler '{type.FullName}' references undeclared operation '{registration.Operation}'.");

            var handlerContract = type.GetInterfaces().SingleOrDefault(candidate =>
                candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IFeatureHandler<>));
            if (handlerContract is null)
                throw new InvalidOperationException(
                    $"Feature handler '{type.FullName}' must implement IFeatureHandler<TResponse>.");
            var responseType = handlerContract.GetGenericArguments()[0];
            var expectedResponseType = operation.ReturnType.GetGenericArguments().Single();
            if (responseType != expectedResponseType)
                throw new InvalidOperationException(
                    $"Feature handler '{type.FullName}' returns '{responseType}', but '{operation.Name}' declares '{expectedResponseType}'.");

            var constructor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    [typeof(LauncherContext)],
                    modifiers: null)
                ?? type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    [typeof(IAccountContext)],
                    modifiers: null);
            var handler = constructor?.Invoke([context])
                ?? throw new InvalidOperationException(
                    $"Feature handler '{type.FullName}' must have a LauncherContext or IAccountContext constructor.");
            var adapterType = typeof(FeatureHandlerAdapter<>).MakeGenericType(responseType);
            var adapter = (IFeatureHandlerAdapter)Activator.CreateInstance(adapterType, handler)!;
            if (!handlers.TryAdd(registration.Operation, adapter))
                throw new InvalidOperationException(
                    $"More than one feature handler is registered for '{registration.Operation}'.");
        }

        var missing = operations.Keys.Except(handlers.Keys, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(
                "Feature operations have no handler: " + string.Join(", ", missing));
    }


    static IEnumerable<Type> EnumerateInterfaces(Type root)
    {
        var seen = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
                continue;
            yield return current;
            foreach (var parent in current.GetInterfaces())
                pending.Push(parent);
        }
    }

    public async Task<TResponse> ExecuteAsync<TResponse>(string operation, object arguments)
    {
        if (!handlers.TryGetValue(operation, out var handler))
            throw new InvalidOperationException($"No feature handler is registered for '{operation}'.");
        if (handler.ResponseType != typeof(TResponse))
            throw new InvalidOperationException(
                $"Feature '{operation}' returns '{handler.ResponseType}', not '{typeof(TResponse)}'.");

        var payload = JsonSerializer.SerializeToElement(arguments, JsonOptions);
        var result = await handler.ExecuteAsync(payload, token);
        return (TResponse)result!;
    }

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    interface IFeatureHandlerAdapter
    {
        Type ResponseType
        {
            get;
        }
        Task<object?> ExecuteAsync(JsonElement arguments, CancellationToken token);
    }

    sealed class FeatureHandlerAdapter<TResponse>(object instance) : IFeatureHandlerAdapter
    {
        readonly IFeatureHandler<TResponse> handler = (IFeatureHandler<TResponse>)instance;

        public Type ResponseType => typeof(TResponse);

        public async Task<object?> ExecuteAsync(JsonElement arguments, CancellationToken token) =>
            await handler.HandleAsync(arguments, token);
    }
}
