using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace ReplicateInator.addons.replicate_inator.scripts;

public static class InputRegistry
{
    private static readonly ConcurrentDictionary<ushort, Type> TypeById = new();
    private static readonly ConcurrentDictionary<Type, ushort> IdByType = new();

    static InputRegistry()
    {
        RegisterAllInputs();
    }

    private static void RegisterAllInputs()
    {
        var interfaceType = typeof(IInputReplication);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var asm in assemblies)
        {
            foreach (var type in asm.GetTypes())
            {
                if (!interfaceType.IsAssignableFrom(type) || !type.IsValueType)
                    continue;

                var attr = type.GetCustomAttribute<InputIdAttribute>();
                if (attr == null) continue;

                TypeById[attr.Id] = type;
                IdByType[type] = attr.Id;
            }
        }
    }

    public static ushort GetId<T>() where T : IInputReplication =>
        IdByType[typeof(T)];

    public static IInputReplication Deserialize(ReadOnlySpan<byte> data)
    {
        ushort id = BitConverter.ToUInt16(data[..2]);
        if (!TypeById.TryGetValue(id, out var type))
            throw new Exception($"Unknown input type id: {id}");

        var method = type.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static);
        
        var payload = data[2..].ToArray(); 
        return (IInputReplication)method.Invoke(null, new object[] { payload })!;
    }
}

[AttributeUsage(AttributeTargets.Struct)]
public class InputIdAttribute : Attribute
{
    public readonly ushort Id;
    public InputIdAttribute(ushort id) => Id = id;
}