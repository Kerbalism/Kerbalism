using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
		static Type persistentTypeAttr = typeof(KsmPersistentType);

		/// <summary> save to `node` all fields and properties in `instance` that have the `[KsmPersistent]` attribute </summary>
		public static void SaveMembers<T>(T instance, ConfigNode node)
		{
			Type instanceType = typeof(T);
			foreach (FieldInfo fi in instanceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (fi.IsDefined(persistentAttr, true))
				{
					if (fi.FieldType.IsDefined(persistentTypeAttr, true))
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
					if (pi.PropertyType.IsDefined(persistentTypeAttr, true))
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
		public static void SaveMember<T>(MemberInfo fieldOrPropertyInfo, T memberInstance, ConfigNode node)
		{
			if (memberInstance == null) return;

			if (memberInstance is IList iList)
			{
				if (iList.Count == 0) return;

				ConfigNode listObjNode = new ConfigNode(fieldOrPropertyInfo.Name);

				// if this is a custom class or struct that has the [KsmPersistentType] attribute, call
				if (iList[0].GetType().IsDefined(persistentTypeAttr, true))
					foreach (object listObj in iList)
						SaveMembers(listObj, listObjNode);
				else
					foreach (object listObj in iList)
						listObjNode.AddValue(listObj, listObjNode);




				node.AddNode(listObjNode);
			}
			else
			{
				if (Serialization.SerializeValue(memberInstance, out string serializedValue))
					node.AddValue(fieldOrPropertyInfo.Name, serializedValue);
				else
					Lib.Log($"ERROR : could not save field or property {fieldOrPropertyInfo.Name} to ConfigNode {node.name}");
			}
		}

		public static void LoadMembers<T>(T instance, ConfigNode node)
		{
			foreach (var item in collection)
			{

			}



			foreach (FieldInfo fi in typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (fi.IsDefined(persistentAttr, true))
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
			else if (true)
			{

			}
		}
	}
}
