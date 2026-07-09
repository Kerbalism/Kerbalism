using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace KERBALISM
{
	internal static class IntegrationReflection
	{
		private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private static readonly object fieldCacheLock = new object();
		private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> fieldCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();

		public static bool GetBool(object target, string name, bool fallback = false)
		{
			FieldInfo field = GetField(target, name);
			if (field != null && field.FieldType == typeof(bool))
				return (bool)field.GetValue(target);

			PropertyInfo property = GetProperty(target, name);
			if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
				return (bool)property.GetValue(target, null);

			MethodInfo method = GetMethod(target, name);
			if (method != null && method.ReturnType == typeof(bool) && method.GetParameters().Length == 0)
				return (bool)method.Invoke(target, null);

			return fallback;
		}

		public static float GetFloat(object target, string name, float fallback = 0f)
		{
			FieldInfo field = GetField(target, name);
			if (field != null && field.FieldType == typeof(float))
				return (float)field.GetValue(target);

			PropertyInfo property = GetProperty(target, name);
			if (property != null && property.PropertyType == typeof(float) && property.GetIndexParameters().Length == 0)
				return (float)property.GetValue(target, null);

			return fallback;
		}

		public static double GetDouble(object target, string name, double fallback = 0d)
		{
			FieldInfo field = GetField(target, name);
			if (field != null && field.FieldType == typeof(double))
				return (double)field.GetValue(target);

			PropertyInfo property = GetProperty(target, name);
			if (property != null && property.PropertyType == typeof(double) && property.GetIndexParameters().Length == 0)
				return (double)property.GetValue(target, null);

			return fallback;
		}

		public static int GetInt(object target, string name, int fallback = 0)
		{
			FieldInfo field = GetField(target, name);
			if (field != null && field.FieldType == typeof(int))
				return (int)field.GetValue(target);

			PropertyInfo property = GetProperty(target, name);
			if (property != null && property.PropertyType == typeof(int) && property.GetIndexParameters().Length == 0)
				return (int)property.GetValue(target, null);

			return fallback;
		}

		public static string GetString(object target, string name, string fallback = "")
		{
			FieldInfo field = GetField(target, name);
			if (field != null && field.FieldType == typeof(string))
				return (string)field.GetValue(target) ?? fallback;

			PropertyInfo property = GetProperty(target, name);
			if (property != null && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
				return (string)property.GetValue(target, null) ?? fallback;

			return fallback;
		}

		public static T GetField<T>(object target, string name, T fallback = default(T))
		{
			FieldInfo field = GetField(target, name);
			if (field != null && typeof(T).IsAssignableFrom(field.FieldType))
				return (T)field.GetValue(target);
			return fallback;
		}

		public static void SetField(object target, string name, object value)
		{
			if (target == null || string.IsNullOrEmpty(name))
				return;

			FieldInfo field = GetField(target, name);
			if (field != null)
			{
				field.SetValue(target, value);
				return;
			}

			PropertyInfo property = GetProperty(target, name);
			if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
				property.SetValue(target, value, null);
		}

		public static object Call(object target, string name, object[] args = null, Type[] parameters = null)
		{
			if (target == null || string.IsNullOrEmpty(name))
				return null;

			MethodInfo method = GetMethod(target, name, parameters);
			return method == null ? null : method.Invoke(target, args);
		}

		public static float EvaluateFloatCurve(object curve, float input, float fallback = 0f)
		{
			if (curve == null)
				return fallback;

			if (curve is FloatCurve floatCurve)
				return floatCurve.Evaluate(input);

			MethodInfo evaluate = curve.GetType().GetMethod("Evaluate", InstanceFlags, null, new[] { typeof(float) }, null);
			if (evaluate != null && evaluate.ReturnType == typeof(float))
				return (float)evaluate.Invoke(curve, new object[] { input });

			return fallback;
		}

		public static IList GetList(object target, string name)
		{
			object value = GetField<object>(target, name);
			return value as IList;
		}

		private static FieldInfo GetField(object target, string name)
		{
			if (target == null || string.IsNullOrEmpty(name))
				return null;

			Type type = target.GetType();
			Dictionary<string, FieldInfo> fields;
			lock (fieldCacheLock)
			{
				if (!fieldCache.TryGetValue(type, out fields))
				{
					fields = new Dictionary<string, FieldInfo>();
					fieldCache[type] = fields;
				}

				FieldInfo field;
				if (!fields.TryGetValue(name, out field))
				{
					for (Type current = type; current != null; current = current.BaseType)
					{
						field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
						if (field != null)
							break;
					}
					fields[name] = field;
				}

				return field;
			}
		}

		private static PropertyInfo GetProperty(object target, string name)
		{
			if (target == null || string.IsNullOrEmpty(name))
				return null;
			return target.GetType().GetProperty(name, InstanceFlags);
		}

		private static MethodInfo GetMethod(object target, string name, Type[] parameters = null)
		{
			if (target == null || string.IsNullOrEmpty(name))
				return null;

			return parameters == null
				? target.GetType().GetMethod(name, InstanceFlags)
				: target.GetType().GetMethod(name, InstanceFlags, null, parameters, null);
		}
	}
}
