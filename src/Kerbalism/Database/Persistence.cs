using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace KERBALISM
{

	[AttributeUsage(AttributeTargets.Field)]
	public class PersistentField : Attribute { }

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class PersistentData : Attribute
	{

	}

	/// <summary>
	/// Use this on a class or struct containing `[PersistentField]` fields, stored in a IList (List, Array...) that itself has the `[PersistentField]` attribute. 
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class PersistentIListData : Attribute
	{
		/// <summary>
		/// <para/>If true, the IList and its elements will be instanciated on load. The class must have a parameterless ctor.
		/// <para/>If false, the IList will be searched (see `instanceIdentifierField`) and the `[PersistentField]` fields values applied on matched elements.
		/// </summary>
		public bool createInstance = true;

		/// <summary>
		/// Only required if `createInstance` is false. Must match the name of a non persistent field defined in the IList elements class or struct.
		/// The field must be assigned with an unique value that stay the same between saves/loads, and is already populated in OnLoad.
		/// </summary>
		public string instanceIdentifierField = string.Empty;

		public PersistentIListData()
		{
			createInstance = true;
			instanceIdentifierField = string.Empty;
		}
	}

	public static class Persistence
	{
		/// <summary> save to `node` all fields in `instance` that have the `[IsPersistent]` attribute </summary>
		public static void SaveFields<T>(T instance, ConfigNode node)
		{
			foreach (FieldInfo fi in typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (fi.IsDefined(typeof(PersistentField), true))
				{
					if (typeof(T).IsDefined(typeof(PersistentData), true))
					{
						ConfigNode dataFieldNode = new ConfigNode(fi.Name);
						SaveFields(fi.GetValue(instance), dataFieldNode);
						node.AddNode(dataFieldNode);
					}
					else if (true)
					{

					}
					else
					{
						SaveField(fi, fi.GetValue(instance), node);
					}
					return;
				}
			}
		}

		/// <summary>
		/// Save the value of `field` to `node` using the name `fieldName`.
		/// This support :
		/// <para/> 1. Types implemented in ConfigNode.TryGetValue().
		/// <para/> 2. Types implementing the `IList` interface (List, Array...), each item is stored as a subnode.
		/// This support lists of a custom class that itself has `[Persistent]` fields, as long as it has a parameterless ctor
		/// </summary>
		public static void SaveField<T>(FieldInfo fieldInfo, T field, ConfigNode node)
		{
			if (Nullable.GetUnderlyingType(typeof(T)) != null && field == null) return;

			if (field is IList iList)
			{
				if (iList.Count == 0) return;

				Type listObjType = iList[0].GetType();
				PersistentIListData pci = (PersistentIListData)listObjType.GetCustomAttributes(true).FirstOrDefault(p => p.GetType() == typeof(PersistentIListData));
				if (pci != null)
				{
					FieldInfo identifierField = listObjType.GetFields(BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.Name == pci.instanceIdentifierField);
					foreach (object listObj in iList)
					{
						ConfigNode listObjNode = new ConfigNode(fieldInfo.Name);
						listObjNode.AddValue(identifierField.Name, identifierField.GetValue(listObj));
						SaveFields(listObj, listObjNode);
						node.AddNode(listObjNode);
					}
				}
				else
				{
					foreach (object listObj in iList)
					{
						ConfigNode listObjNode = new ConfigNode(fieldInfo.Name);
						SaveFields(listObj, listObjNode);
						node.AddNode(listObjNode);
					}
				}
			}
			else
			{

				node.AddValue(fieldInfo.Name, field);
			}
		}

		public static void LoadFields<T>(T instance, ConfigNode node)
		{
			foreach (FieldInfo fi in typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (fi.GetCustomAttributes(false).Any(p => p.GetType() == typeof(Persistent)))
				{
					LoadField(fi, instance, node);
				}
			}
		}

		public static void LoadField<T>(FieldInfo fieldInfo, T field, ConfigNode node)
		{
			Type fieldType = fieldInfo.FieldType;
			if (fieldType.IsAssignableFrom(typeof(IList<>)))
			{
				if (!node.HasNode(fieldInfo.Name)) return;

				Type listObjectType = fieldType.GetInterfaces().First(p => p == typeof(IList<>)).GetGenericArguments().Single();
				PersistentIListData pci = (PersistentIListData)listObjectType.GetCustomAttributes(true).FirstOrDefault(p => p.GetType() == typeof(PersistentIListData));
				IList iList;
				if (pci != null)
				{
					iList = (IList)field;

					FieldInfo identifierField = listObjectType.GetFields(BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.Name == pci.instanceIdentifierField);

					foreach (object listObject in iList)
					{
						ConfigNode listObjectNode = node.GetNode(fieldInfo.Name, identifierField.Name, (string)identifierField.GetValue(listObject));
						if (listObjectNode != null)
						{
							LoadFields(listObject, listObjectNode);
						}
					}
				}
				else
				{
					iList = (IList)Activator.CreateInstance(fieldType);

					foreach (ConfigNode listObjectNode in node.GetNodes(fieldInfo.Name))
					{
						object listObject = Activator.CreateInstance(listObjectType);
						LoadFields(listObject, listObjectNode);
						iList.Add(listObject);
					}
					fieldInfo.SetValue(field, iList);
				}
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
