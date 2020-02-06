using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public static class Serialization
	{
		private static Type enumType = typeof(Enum);

		public static bool SerializeValue<T>(T value, out string serializedValue)
		{
			Type typeOfValue = value.GetType();
			if (enumType.IsAssignableFrom(typeOfValue))
			{
				serializedValue = value.ToString();
				return true;
			}
			else
			{
				if (parsers.TryGetValue(typeOfValue, out object parser))
				{
					serializedValue = ((ValueParser<T>)parser).Serialize(value);
					return true;
				}
			}

			Lib.Log($"ERROR : could not serialize value {value.ToString()}, the type {typeOfValue.Name} is unsupported");
			serializedValue = string.Empty;
			return false;
		}

		public static bool DeserializeValue<T>(string serializedValue, out T value)
		{
			Type typeOfValue = typeof(T);
			if (enumType.IsAssignableFrom(typeOfValue))
			{
				try
				{
					value = (T)Enum.Parse(typeOfValue, serializedValue);
					return true;
				}
				catch
				{
					Lib.Log($"ERROR : could not deserialize enum value {serializedValue} to enum type {typeOfValue.Name}");
					value = default;
					return false;
				}
			}
			else
			{
				if (parsers.TryGetValue(typeOfValue, out object parser))
				{
					if (((ValueParser<T>)parser).Deserialize(serializedValue, out value))
					{
						return true;
					}
				}
			}
			Lib.Log($"ERROR : could not deserialize value {serializedValue} to {typeOfValue.Name}");
			value = default;
			return false;
		}

		public static ValueParser<T> GetParser<T>()
		{
			Type type = typeof(T);

			if (enumType.IsAssignableFrom(type))
				return (ValueParser<T>)parsers[typeof(Enum)];

			if (!parsers.TryGetValue(type, out object parser))
				return null;

			return (ValueParser<T>)parser;
		}

		public static bool CanParse<T>()
		{
			Type type = typeof(T);

			if (enumType.IsAssignableFrom(type))
				return true;

			return parsers.ContainsKey(type);
		}

		private static Dictionary<Type, object> parsers = new Dictionary<Type, object>()
		{
			{ typeof(Enum), new EnumParser() },
			{ typeof(string), new StringParser() },
			{ typeof(bool), new BoolParser() },
			{ typeof(byte), new ByteParser() },
			{ typeof(char), new CharParser() },
			{ typeof(decimal), new DecimalParser() },
			{ typeof(double), new DoubleParser() },
			{ typeof(short), new ShortParser() },
			{ typeof(int), new IntParser() },
			{ typeof(long), new LongParser() },
			{ typeof(sbyte), new SbyteParser() },
			{ typeof(float), new FloatParser() },
			{ typeof(ushort), new UshortParser() },
			{ typeof(uint), new UintParser() },
			{ typeof(ulong), new UlongParser() },
			{ typeof(Guid), new GuidParser() },

			{ typeof(Vector2), new Vector2Parser() },
			{ typeof(Vector3), new Vector3Parser() },
			{ typeof(Vector3d), new Vector3dParser() },
			{ typeof(Vector4), new Vector4Parser() },
			{ typeof(Quaternion), new QuaternionParser() },
			{ typeof(QuaternionD), new QuaternionDParser() },
			{ typeof(Matrix4x4), new Matrix4x4Parser() },
			{ typeof(Color), new ColorParser() },
			{ typeof(Color32), new Color32Parser() },
		};

		public class ValueParser<T>
		{
			protected Type typeOfValue;

			public ValueParser()
			{
				typeOfValue = typeof(T);
			}

			public virtual string Serialize(T value) => value.ToString();

			public virtual bool Deserialize(string strValue, out T value)
			{
				try
				{
					value = (T)Convert.ChangeType(strValue, typeOfValue);
					return true;
				}
				catch
				{
					value = default;
					return false;
				}
			}
		}

		#region system types parsers

		public class EnumParser : ValueParser<object>
		{
			public override string Serialize(object value) => value.ToString();
			public override bool Deserialize(string strValue, out object value)
			{
				value = default;
				try
				{
					value = Enum.Parse(value.GetType(), strValue);
					return true;
				}
				catch
				{
					Lib.Log($"ERROR : could not deserialize enum value {strValue} to enum type {value.GetType().Name}");
					return false;
				}
			}
		}

		private class StringParser : ValueParser<string>
		{
			public override string Serialize(string value) => value;
			public override bool Deserialize(string strValue, out string value)
			{
				value = strValue;
				return value != null;
			}
		}

		public class BoolParser : ValueParser<bool>
		{
			public override string Serialize(bool value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class ByteParser : ValueParser<byte>
		{
			public override string Serialize(byte value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class CharParser : ValueParser<char>
		{
			public override string Serialize(char value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class DecimalParser : ValueParser<decimal>
		{
			public override string Serialize(decimal value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class DoubleParser : ValueParser<double>
		{
			public override string Serialize(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
		}

		public class ShortParser : ValueParser<short>
		{
			public override string Serialize(short value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class IntParser : ValueParser<int>
		{
			public override string Serialize(int value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class LongParser : ValueParser<long>
		{
			public override string Serialize(long value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class SbyteParser : ValueParser<sbyte>
		{
			public override string Serialize(sbyte value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class FloatParser : ValueParser<float>
		{
			public override string Serialize(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
		}

		public class UshortParser : ValueParser<ushort>
		{
			public override string Serialize(ushort value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class UintParser : ValueParser<uint>
		{
			public override string Serialize(uint value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class UlongParser : ValueParser<ulong>
		{
			public override string Serialize(ulong value) => value.ToString(CultureInfo.InvariantCulture);
		}

		public class GuidParser : ValueParser<Guid>
		{
			public override string Serialize(Guid value) => value.ToString("N", CultureInfo.InvariantCulture);
			public override bool Deserialize(string strValue, out Guid value) => Guid.TryParseExact(strValue, "N", out value);
		}

		#endregion

		#region Unity/KSP types parsers

		public class Vector2Parser : ValueParser<Vector2>
		{
			public override bool Deserialize(string strValue, out Vector2 value) => ParseExtensions.TryParseVector2(strValue, out value);
			public override string Serialize(Vector2 value) => ConfigNode.WriteVector(value);
		}

		public class Vector3Parser : ValueParser<Vector3>
		{
			public override bool Deserialize(string strValue, out Vector3 value) => ParseExtensions.TryParseVector3(strValue, out value);
			public override string Serialize(Vector3 value) => ConfigNode.WriteVector(value);
		}

		public class Vector3dParser : ValueParser<Vector3d>
		{
			public override bool Deserialize(string strValue, out Vector3d value) => ParseExtensions.TryParseVector3d(strValue, out value);
			public override string Serialize(Vector3d value) => ConfigNode.WriteVector(value);
		}

		public class Vector4Parser : ValueParser<Vector4>
		{
			public override bool Deserialize(string strValue, out Vector4 value) => ParseExtensions.TryParseVector4(strValue, out value);
			public override string Serialize(Vector4 value) => ConfigNode.WriteVector(value);
		}

		public class QuaternionParser : ValueParser<Quaternion>
		{
			public override bool Deserialize(string strValue, out Quaternion value) => ParseExtensions.TryParseQuaternion(strValue, out value);
			public override string Serialize(Quaternion value) => ConfigNode.WriteQuaternion(value);
		}

		public class QuaternionDParser : ValueParser<QuaternionD>
		{
			public override bool Deserialize(string strValue, out QuaternionD value) => ParseExtensions.TryParseQuaternionD(strValue, out value);
			public override string Serialize(QuaternionD value) => ConfigNode.WriteQuaternion(value);
		}

		public class Matrix4x4Parser : ValueParser<Matrix4x4>
		{
			public override bool Deserialize(string strValue, out Matrix4x4 value)
			{
				value = ConfigNode.ParseMatrix4x4(strValue);
				return true;
			}
			public override string Serialize(Matrix4x4 value) => ConfigNode.WriteMatrix4x4(value);
		}

		public class ColorParser : ValueParser<Color>
		{
			public override bool Deserialize(string strValue, out Color value) => ParseExtensions.TryParseColor(strValue, out value);
			public override string Serialize(Color value) => ConfigNode.WriteColor(value);
		}

		public class Color32Parser : ValueParser<Color32>
		{
			public override bool Deserialize(string strValue, out Color32 value) => ParseExtensions.TryParseColor32(strValue, out value);
			public override string Serialize(Color32 value) => ConfigNode.WriteColor(value);
		}

		#endregion

	}
}
