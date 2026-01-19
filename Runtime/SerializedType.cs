using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Neonalig.Core
{
    [Serializable]
    public sealed class SerializedType
    {
        [FormerlySerializedAs("serializedTypeString")]
        [SerializeField]
        string _serializedTypeString = string.Empty;

        public string SerializedTypeString => _serializedTypeString;

        public Type? Type => string.IsNullOrEmpty(_serializedTypeString) ? null : SerializedTypeData.FromSerializedString(_serializedTypeString).ToType();

        public SerializedTypeData TypeData => SerializedTypeData.FromSerializedString(_serializedTypeString);

        public SerializedType(Type? type)
        {
            _serializedTypeString = type == null ? string.Empty : new SerializedTypeData(type).ToSerializedString();
        }

        public SerializedType() { }

        public static SerializedType FromString(string serializedTypeString)
        {
            return new SerializedType { _serializedTypeString = serializedTypeString };
        }

        public string ToSerializedString() => _serializedTypeString;

#pragma warning disable 0809 // Obsolete member overrides non-obsolete member
        /// <inheritdoc />
        [Obsolete("Ambiguous case; Did you mean ToSerializedString()? or Type.ToString()?")]
        public override string ToString()
        {
            return _serializedTypeString;
        }
#pragma warning restore 0809 // Obsolete member overrides non-obsolete member

        public static implicit operator Type?(SerializedType serializedType) => serializedType.Type;
        public static implicit operator SerializedType(Type type) => new(type);
    }

    [Serializable]
    public struct SerializedTypeData
    {
        [SerializeField]
        public string TypeName;

        [SerializeField]
        public string GenericTypeName;

        [SerializeField]
        public bool IsGeneric;

        public SerializedTypeData(string typeName, string genericTypeName, bool isGeneric)
        {
            TypeName = typeName;
            GenericTypeName = genericTypeName;
            IsGeneric = isGeneric;
        }

        public SerializedTypeData(Type type)
        {
            TypeName = string.Empty;
            IsGeneric = type.ContainsGenericParameters;
            if (IsGeneric && type.IsGenericType)
            {
                TypeName = ToShortTypeName(type.GetGenericTypeDefinition());
            }
            else
            {
                int num = !IsGeneric ? 0 : (type.IsArray ? 1 : 0);
                TypeName = num == 0 ? (!IsGeneric ? ToShortTypeName(type) : "T") : "T[]";
            }
            GenericTypeName = string.Empty;
        }

        public static SerializedTypeData FromSerializedString(string serializedTypeString)
        {
            return new SerializedTypeData(
                serializedTypeString[..serializedTypeString.IndexOf('#')],
                serializedTypeString.Substring(
                    serializedTypeString.IndexOf('#') + 1,
                    serializedTypeString.IndexOf('#', serializedTypeString.IndexOf('#') + 1) - serializedTypeString.IndexOf('#') - 1
                ),
                serializedTypeString[^1] == '1'
            );
        }

        public string ToSerializedString()
        {
            return TypeName + "#" + GenericTypeName + "#" + (IsGeneric ? "1" : "0");
        }

        public Type? ToType()
        {
            if (IsGeneric)
                return null;

            try
            {
                return Type.GetType(TypeName, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get type from SerializedTypeData: {TypeName}. See below log for more info.");
                Debug.LogException(ex);
                return null;
            }
        }

        public bool IsBaseTypeGeneric()
        {
            return IsGeneric || GenericTypeName != string.Empty;
        }

        public SerializedTypeData WithGenericArgumentType(Type type)
        {
            if (!IsGeneric)
            {
                if (IsBaseTypeGeneric())
                    throw new ArgumentException("Trying to set a different generic type. Reset old one first.");
                throw new ArgumentException("Trying to set generic argument type for non generic type.");
            }
            return new SerializedTypeData(
                TypeName switch
                {
                    "T" => ToShortTypeName(type),
                    "T[]" => ToShortTypeName(type.MakeArrayType()),
                    _ => ToShortTypeName(Type.GetType(TypeName, true).GetGenericTypeDefinition().MakeGenericType(type))
                },
                GenericTypeName,
                false
            );
        }

        public SerializedTypeData WithResetGenericArgumentType()
        {
            return new SerializedTypeData(
                !string.IsNullOrEmpty(GenericTypeName) ? GenericTypeName : throw new ArgumentException("Cannot reset generic argument type, previous generic type unknown."),
                string.Empty,
                true
            );
        }

        public bool CanAssignFromGenericType(Type t)
        {
            if (!IsGeneric)
                return false;
            if (!t.IsGenericType)
                return TypeName is "T" or "T[]";
            if (TypeName is "T" or "T[]")
                return false;
            Type[] genericArguments = t.GetGenericArguments();
            return genericArguments.Length == 1 && !genericArguments[0].IsGenericType && t.GetGenericTypeDefinition() == GetGenericTypeDefinition();
        }

        Type? GetGenericTypeDefinition()
        {
            return ToType()?.GetGenericTypeDefinition();
        }

        static string StripTypeNameString(string str, int index)
        {
            int index1 = index + 1;
            while (index1 < str.Length && str[index1] != ',' && str[index1] != ']')
                ++index1;
            return str.Remove(index, index1 - index);
        }

        static string StripAllFromTypeNameString(string str, string toStrip)
        {
            for (int index = str.IndexOf(toStrip, StringComparison.Ordinal); index != -1; index = str.IndexOf(toStrip, index, StringComparison.Ordinal))
                str = StripTypeNameString(str, index);
            return str;
        }

        static string ToShortTypeName(Type t)
        {
            string? assemblyQualifiedName = t.AssemblyQualifiedName;
            return string.IsNullOrEmpty(assemblyQualifiedName) ? string.Empty : StripAllFromTypeNameString(StripAllFromTypeNameString(StripAllFromTypeNameString(assemblyQualifiedName, ", Version"), ", Culture"), ", PublicKeyToken");
        }

        static string? SafeTypeName(Type? type)
        {
            return type?.FullName is { } fullName ? fullName.Replace('+', '.') : null;
        }

        public string GetFullName()
        {
            if (!IsGeneric)
                return SafeTypeName(ToType()) ?? string.Empty;
            return GetGenericTypeDefinition() == typeof(List<>) ? $"System.Collections.Generic.List<{SafeTypeName(ToType()?.GetGenericArguments().FirstOrDefault())}>" : throw new ArgumentException("Internal error: got unsupported generic type");
        }
    }

    public sealed class MustDeriveAttribute : PropertyAttribute
    {
        public readonly Type BaseType;

        public MustDeriveAttribute(Type baseType)
        {
            BaseType = baseType;
        }
    }
}
