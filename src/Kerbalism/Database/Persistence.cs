using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace KERBALISM
{

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class KsmPersistent : Attribute { }

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class KsmPersistentType : Attribute { }

	public static class Persistence
	{
		static Type persistentAttr = typeof(KsmPersistent);
		static Type PersistentTypeAttr = typeof(KsmPersistentType);

		/// <summary> save to `node` all fields and properties in `instance` that have the `[KsmPersistent]` attribute </summary>
		public static void SaveMembers<T>(T instance, ConfigNode node)
		{
			Type instanceType = typeof(T);
			foreach (FieldInfo fi in instanceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (fi.IsDefined(persistentAttr, true))
				{
					if (fi.FieldType.IsDefined(PersistentTypeAttr, true))
					{
						ConfigNode dataFieldNode = new ConfigNode(fi.Name);
						SaveMembers(fi.GetValue(instance), dataFieldNode);
						node.AddNode(dataFieldNode);
					}
					else
					{
						SaveMember(fi, fi.GetValue(instance), node);
					}
				}
			}
			foreach (PropertyInfo pi in instanceType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (pi.IsDefined(persistentAttr, true))
				{
					if (pi.PropertyType.IsDefined(PersistentTypeAttr, true))
					{
						ConfigNode dataFieldNode = new ConfigNode(pi.Name);
						SaveMembers(pi.GetValue(instance, null), dataFieldNode);
						node.AddNode(dataFieldNode);
					}
					else
					{
						SaveMember(pi, pi.GetValue(instance, null), node);
					}
				}
			}



		}

		/// <summary>
		/// Save the value of `field` to `node` using the name `fieldName`.
		/// This support :
		/// <para/> 1. Types implemented in ConfigNode.TryGetValue().
		/// <para/> 2. Types implementing the `IList` interface (List, Array...), each item is stored as a subnode.
		/// This support lists of a custom class that itself has `[Persistent]` fields, as long as the class has a parameterless ctor
		/// </summary>
		public static void SaveMember<T>(MemberInfo fieldOrPropertyInfo, T field, ConfigNode node)
		{
			if (field == null) return;

			if (field is IList iList)
			{
				if (iList.Count == 0) return;

				foreach (object listObj in iList)
				{
					ConfigNode listObjNode = new ConfigNode(fieldOrPropertyInfo.Name);
					SaveMembers(listObj, listObjNode);
					node.AddNode(listObjNode);
				}
			}
			else
			{
				node.AddValue(fieldOrPropertyInfo.Name, field);
			}
		}

		public static void LoadMembers<T>(T instance, ConfigNode node)
		{
			foreach (FieldInfo fi in typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (fi.GetCustomAttributes(false).Any(p => p.GetType() == typeof(Persistent)))
				{
					LoadMember(fi, instance, node);
				}
			}
		}

		public static void LoadMember<T>(MemberInfo fieldOrPropertyInfo, T field, ConfigNode node)
		{
			Type fieldType = typeof(T);
			object loadedMember;
				
			if (typeof(IList).IsAssignableFrom(fieldType))
			{
				Type elementType = fieldType.GetElementType(); // will work only for arrays
				if (elementType == null)
				{
					elementType = fieldType.GetGenericArguments().FirstOrDefault(); // should work for any other collection
				}
				if (elementType == null)
				{
					Lib.Log($"ERROR : load failed for field or property {fieldOrPropertyInfo.Name} of type {fieldType.ToString()} : could not find the elements type");
					return;
				}
					
				IList iList = (IList)Activator.CreateInstance(fieldType);

				foreach (ConfigNode listObjectNode in node.GetNodes(fieldOrPropertyInfo.Name))
				{
					object listObject = Activator.CreateInstance(elementType);
					LoadMembers(listObject, listObjectNode);
					iList.Add(listObject);
				}

				loadedMember = iList;
			}
			else
			{
				string stringValue = node.GetValue(fieldInfo.Name);
				if (string.IsNullOrEmpty(stringValue)) return;

				if (fieldType == typeof(string))
				{
					fieldInfo.SetValue(field, stringValue);
				}
				else if (fieldType.IsAssignableFrom(typeof(Enum)))
				{
					if (!Enum.IsDefined(fieldType, stringValue)) return;
					fieldInfo.SetValue(field, Enum.Parse(fieldType, stringValue));
				}
				else if (fieldType == typeof(float))
				{
					if (!float.TryParse(stringValue, out float value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(double))
				{
					if (!double.TryParse(stringValue, out double value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(int))
				{
					if (!int.TryParse(stringValue, out int value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(uint))
				{
					if (!uint.TryParse(stringValue, out uint value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(long))
				{
					if (!long.TryParse(stringValue, out long value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(ulong))
				{
					if (!ulong.TryParse(stringValue, out ulong value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(bool))
				{
					if (!bool.TryParse(stringValue, out bool value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Vector2))
				{
					if (!ParseExtensions.TryParseVector2(stringValue, out Vector2 value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Vector2d))
				{
					if (!ParseExtensions.TryParseVector2d(stringValue, out Vector2d value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Vector3))
				{
					if (!ParseExtensions.TryParseVector3(stringValue, out Vector3 value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Vector3d))
				{
					if (!ParseExtensions.TryParseVector3d(stringValue, out Vector3d value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Vector4))
				{
					if (!ParseExtensions.TryParseVector4(stringValue, out Vector4 value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Vector4d))
				{
					if (!ParseExtensions.TryParseVector4d(stringValue, out Vector4d value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Quaternion))
				{
					if (!ParseExtensions.TryParseQuaternion(stringValue, out Quaternion value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(QuaternionD))
				{
					if (!ParseExtensions.TryParseQuaternionD(stringValue, out QuaternionD value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Rect))
				{
					if (!ParseExtensions.TryParseRect(stringValue, out Rect value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Color))
				{
					if (!ParseExtensions.TryParseColor(stringValue, out Color value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Color32))
				{
					if (!ParseExtensions.TryParseColor32(stringValue, out Color32 value)) return;
					fieldInfo.SetValue(field, value);
				}
				else if (fieldType == typeof(Guid))
				{
					Guid value;
					try { value = new Guid(stringValue); } catch { return; }
					fieldInfo.SetValue(field, value);
				}

			}
		}
	}
}
