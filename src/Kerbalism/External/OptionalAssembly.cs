using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace KERBALISM
{
	internal sealed class OptionalAssembly
	{
		private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
		private readonly Dictionary<string, FieldInfo> fieldCache = new Dictionary<string, FieldInfo>();
		private readonly Dictionary<string, PropertyInfo> propertyCache = new Dictionary<string, PropertyInfo>();
		private readonly Dictionary<string, MethodInfo> methodCache = new Dictionary<string, MethodInfo>();

		public OptionalAssembly(string assemblyName)
		{
			AssemblyName = assemblyName;
		}

		public string AssemblyName { get; }

		public bool Installed => Lib.HasAssembly(AssemblyName);

		public Type Type(string fullName)
		{
			if (string.IsNullOrEmpty(fullName) || !Installed)
				return null;

			Type type;
			if (!typeCache.TryGetValue(fullName, out type))
			{
				type = AccessTools.TypeByName(fullName);
				typeCache[fullName] = type;
			}
			return type;
		}

		public bool IsModule(PartModule module, string fullName)
		{
			Type type = Type(fullName);
			return type != null && module != null && type.IsInstanceOfType(module);
		}

		public FieldInfo Field(Type type, string name)
		{
			if (type == null || string.IsNullOrEmpty(name))
				return null;

			string key = type.FullName + "::field::" + name;
			FieldInfo field;
			if (!fieldCache.TryGetValue(key, out field))
			{
				field = type.GetField(name, Flags);
				fieldCache[key] = field;
			}
			return field;
		}

		public PropertyInfo Property(Type type, string name)
		{
			if (type == null || string.IsNullOrEmpty(name))
				return null;

			string key = type.FullName + "::property::" + name;
			PropertyInfo property;
			if (!propertyCache.TryGetValue(key, out property))
			{
				property = type.GetProperty(name, Flags);
				propertyCache[key] = property;
			}
			return property;
		}

		public MethodInfo Method(Type type, string name, Type[] parameters = null)
		{
			if (type == null || string.IsNullOrEmpty(name))
				return null;

			string key = type.FullName + "::method::" + name + "::" + SignatureKey(parameters);
			MethodInfo method;
			if (!methodCache.TryGetValue(key, out method))
			{
				method = parameters == null ? type.GetMethod(name, Flags) : type.GetMethod(name, Flags, null, parameters, null);
				methodCache[key] = method;
			}
			return method;
		}

		public T Get<T>(object instance, string name, T fallback = default(T))
		{
			if (instance == null)
				return fallback;

			Type type = instance.GetType();
			FieldInfo field = Field(type, name);
			if (field != null)
			{
				object value = field.GetValue(instance);
				if (value is T)
					return (T)value;
			}

			PropertyInfo property = Property(type, name);
			if (property != null && property.GetIndexParameters().Length == 0)
			{
				object value = property.GetValue(instance, null);
				if (value is T)
					return (T)value;
			}

			return fallback;
		}

		public void Set<T>(object instance, string name, T value)
		{
			if (instance == null)
				return;

			Type type = instance.GetType();
			FieldInfo field = Field(type, name);
			if (field != null)
			{
				field.SetValue(instance, value);
				return;
			}

			PropertyInfo property = Property(type, name);
			if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
				property.SetValue(instance, value, null);
		}

		public object Call(object instance, string name, Type[] parameters = null, object[] args = null)
		{
			if (instance == null)
				return null;

			MethodInfo method = Method(instance.GetType(), name, parameters);
			return method == null ? null : method.Invoke(instance, args);
		}

		private static string SignatureKey(Type[] parameters)
		{
			if (parameters == null || parameters.Length == 0)
				return "";

			string[] names = new string[parameters.Length];
			for (int i = 0; i < parameters.Length; i++)
				names[i] = parameters[i] == null ? "" : parameters[i].FullName;
			return string.Join("|", names);
		}
	}
}
