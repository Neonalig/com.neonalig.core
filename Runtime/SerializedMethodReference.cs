using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Neonalig.Core
{
    [Serializable]
    public sealed class SerializedMethodReference
    {
        [FormerlySerializedAs("declaringType")]
        [SerializeField] private string _declaringType = string.Empty;
        [FormerlySerializedAs("methodName")]
        [SerializeField] private string _methodName = string.Empty;
        [FormerlySerializedAs("argumentTypes")]
        [SerializeField] private string[] _argumentTypes = Array.Empty<string>();
        [FormerlySerializedAs("genericParameters")]
        [SerializeField] private string[] _genericParameters = Array.Empty<string>();

        private const BindingFlags _defaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        public MethodInfo? GetMethod()
        {
            if (string.IsNullOrEmpty(this._declaringType))
                return null;

            Type? declaringType = SerializedType.FromString(this._declaringType).Type;
            if (declaringType == null)
            {
                Debug.LogError($"Type '{this._declaringType}' could not be found.");
                return null;
            }

            Type[] argumentTypes = Array.ConvertAll(this._argumentTypes, t => SerializedType.FromString(t).Type ?? throw new InvalidOperationException($"Type '{t}' could not be found."));
            MethodInfo? method = declaringType.GetMethod(_methodName, _genericParameters.Length, _defaultBindingFlags, null, argumentTypes, new ParameterModifier[0]);
            if (method == null)
                Debug.LogError($"Method '{_methodName}' with arguments '{string.Join<Type>(", ", argumentTypes)}' could not be found in type '{declaringType}'.");

            return method;
        }

        public static implicit operator SerializedMethodReference(MethodInfo method) => new(method);
        public static implicit operator MethodInfo?(SerializedMethodReference reference) => reference.GetMethod();

        [UsedImplicitly]
        public SerializedMethodReference()
        {
        }

        public SerializedMethodReference(MethodInfo method)
        {
            _declaringType = new SerializedType(method.DeclaringType).ToSerializedString();
            _methodName = method.Name;
            Type[] types = Array.ConvertAll(method.GetParameters(), p => p.ParameterType);
            _argumentTypes = Array.ConvertAll(types, t => new SerializedType(t).ToSerializedString());
            _genericParameters = Array.ConvertAll(method.GetGenericArguments(), t => new SerializedType(t).ToSerializedString());
        }

        public override string ToString()
        {
            return $"{_declaringType}.{_methodName}({string.Join(", ", _argumentTypes)})";
        }
    }

    public sealed class BindingFlagsAttribute : Attribute
    {
        public BindingFlags Flags { get; }

        public BindingFlagsAttribute(BindingFlags flags)
        {
            Flags = flags;
        }
    }

    public sealed class AssembliesAttribute : Attribute
    {
        public IReadOnlyList<string> Assemblies { get; }

        public AssembliesAttribute(params string[] assemblies)
        {
            Assemblies = assemblies;
        }
    }
}
