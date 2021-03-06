using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{

	/// <summary>A wrapper around a method to use it as a patch (for example a Prefix)</summary>
	public class HarmonyMethod
	{
		/// <summary>The original method</summary>
		public MethodInfo method; // need to be called 'method'

		/// <summary>Class/type declaring this patch</summary>
		public Type declaringType;
		/// <summary>Patch method name</summary>
		public string methodName;
		/// <summary>Patch method type</summary>
		public MethodType? methodType;
		/// <summary>Argument types of the patch method</summary>
		public Type[] argumentTypes;
		/// <summary><see cref="Priority"/> of the patch</summary>
		public int priority = -1;
		/// <summary>Install this patch before patches with there Harmony IDs</summary>
		public string[] before;
		/// <summary>Install this patch after patches with there Harmony IDs</summary>
		public string[] after;
		/// <summary>Reverse patch type, see <see cref="HarmonyReversePatchType"/></summary>
		public HarmonyReversePatchType? reversePatchType;
		/// <summary>Create debug output for this patch</summary>
		public bool? debug;

		/// <summary>Default constructor</summary>
		public HarmonyMethod()
		{
		}

		void ImportMethod(MethodInfo theMethod)
		{
			method = theMethod;
			if (method != null)
			{
				var infos = HarmonyMethodExtensions.GetFromMethod(method);
				if (infos != null)
					Merge(infos).CopyTo(this);
			}
		}

		/// <summary>Creates a patch from a given method</summary>
		/// <param name="method">The original method</param>
		///
		public HarmonyMethod(MethodInfo method)
		{
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			ImportMethod(method);
		}

		/// <summary>Creates a patch from a given method</summary>
		/// <param name="method">The original method</param>
		/// <param name="priority">The patch priority</param>
		/// <param name="before">A list of harmony IDs that should come after this patch</param>
		/// <param name="after">A list of harmony IDs that should come before this patch</param>
		/// <param name="debug">Set to true to generate debug output</param>
		///
		public HarmonyMethod(MethodInfo method, int priority = -1, string[] before = null, string[] after = null, bool? debug = null)
		{
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			ImportMethod(method);
			this.priority = priority;
			this.before = before;
			this.after = after;
			this.debug = debug;
		}

		/// <summary>Creates a patch from a given method</summary>
		/// <param name="methodType">The patch method type</param>
		/// <param name="methodName">The patch method name</param>
		/// <param name="argumentTypes">The optional argument types of the patch method (for overloaded methods)</param>
		///
		public HarmonyMethod(Type methodType, string methodName, Type[] argumentTypes = null)
		{
			var result = AccessTools.Method(methodType, methodName, argumentTypes);
			if (result == null)
				throw new ArgumentException($"Cannot not find method for type {methodType} and name {methodName} and parameters {argumentTypes?.Description()}");
			ImportMethod(result);
		}

		/// <summary>Gets the names of all internal patch info fields</summary>
		/// <returns>A list of field names</returns>
		///
		public static List<string> HarmonyFields()
		{
			return AccessTools
				.GetFieldNames(typeof(HarmonyMethod))
				.Where(s => s != "method")
				.ToList();
		}

		/// <summary>Merges annotations</summary>
		/// <param name="attributes">The annotations</param>
		/// <returns>A merged annotation</returns>
		///
		public static HarmonyMethod Merge(List<HarmonyMethod> attributes)
		{
			var result = new HarmonyMethod();
			if (attributes == null) return result;
			var resultTrv = Traverse.Create(result);
			attributes.ForEach(attribute =>
			{
				var trv = Traverse.Create(attribute);
				HarmonyFields().ForEach(f =>
				{
					var val = trv.Field(f).GetValue();
					if (val != null)
						HarmonyMethodExtensions.SetValue(resultTrv, f, val);
				});
			});
			return result;
		}

		/// <summary>Returns a string that represents the annotation</summary>
		/// <returns>A string representation</returns>
		///
		public override string ToString()
		{
			var result = "";
			var trv = Traverse.Create(this);
			HarmonyFields().ForEach(f =>
			{
				if (result.Length > 0) result += ", ";
				result += $"{f}={trv.Field(f).GetValue()}";
			});
			return $"HarmonyMethod[{result}]";
		}

		// used for error reporting
		internal string Description()
		{
			var cName = declaringType != null ? declaringType.FullName : "undefined";
			var mName = methodName ?? "undefined";
			var tName = methodType.HasValue ? methodType.Value.ToString() : "undefined";
			var aName = argumentTypes != null ? argumentTypes.Description() : "undefined";
			return $"(class={cName}, methodname={mName}, type={tName}, args={aName})";
		}
	}

	/// <summary>Annotation extensions</summary>
	public static class HarmonyMethodExtensions
	{
		internal static void SetValue(Traverse trv, string name, object val)
		{
			if (val == null) return;
			var fld = trv.Field(name);
			if (name == nameof(HarmonyMethod.methodType) || name == nameof(HarmonyMethod.reversePatchType))
			{
				var enumType = Nullable.GetUnderlyingType(fld.GetValueType());
				val = Enum.ToObject(enumType, (int)val);
			}
			_ = fld.SetValue(val);
		}

		/// <summary>Copies annotation information</summary>
		/// <param name="from">from</param>
		/// <param name="to">to</param>
		///
		public static void CopyTo(this HarmonyMethod from, HarmonyMethod to)
		{
			if (to == null) return;
			var fromTrv = Traverse.Create(from);
			var toTrv = Traverse.Create(to);
			HarmonyMethod.HarmonyFields().ForEach(f =>
			{
				var val = fromTrv.Field(f).GetValue();
				if (val != null)
					SetValue(toTrv, f, val);
			});
		}

		/// <summary>Clones an annotation</summary>
		/// <param name="original">The annotation to clone</param>
		/// <returns>A copy of the annotation</returns>
		///
		public static HarmonyMethod Clone(this HarmonyMethod original)
		{
			var result = new HarmonyMethod();
			original.CopyTo(result);
			return result;
		}

		/// <summary>Merges annotations</summary>
		/// <param name="master">The master</param>
		/// <param name="detail">The detail</param>
		/// <returns>A new, merged copy</returns>
		///
		public static HarmonyMethod Merge(this HarmonyMethod master, HarmonyMethod detail)
		{
			if (detail == null) return master;
			var result = new HarmonyMethod();
			var resultTrv = Traverse.Create(result);
			var masterTrv = Traverse.Create(master);
			var detailTrv = Traverse.Create(detail);
			HarmonyMethod.HarmonyFields().ForEach(f =>
			{
				var baseValue = masterTrv.Field(f).GetValue();
				var detailValue = detailTrv.Field(f).GetValue();
				SetValue(resultTrv, f, detailValue ?? baseValue);
			});
			return result;
		}

		static HarmonyMethod GetHarmonyMethodInfo(object attribute)
		{
			var f_info = attribute.GetType().GetField(nameof(HarmonyAttribute.info), AccessTools.all);
			if (f_info == null) return null;
			if (f_info.FieldType.FullName != typeof(HarmonyMethod).FullName) return null;
			var info = f_info.GetValue(attribute);
			return AccessTools.MakeDeepCopy<HarmonyMethod>(info);
		}

		/// <summary>Gets all annotations on a class</summary>
		/// <param name="type">The class</param>
		/// <returns>All annotations</returns>
		///
		public static List<HarmonyMethod> GetFromType(Type type)
		{
			return type.GetCustomAttributes(true)
						.Select(attr => GetHarmonyMethodInfo(attr))
						.Where(info => info != null)
						.ToList();
		}

		/// <summary>Gets all annotations on a class in merged form</summary>
		/// <param name="type">The class</param>
		/// <returns>The merged HarmonyMethod</returns>
		///
		public static HarmonyMethod GetMergedFromType(Type type)
		{
			return HarmonyMethod.Merge(GetFromType(type));
		}

		/// <summary>Gets all annotations on a method</summary>
		/// <param name="method">The method</param>
		/// <returns>All annotations</returns>
		///
		public static List<HarmonyMethod> GetFromMethod(MethodBase method)
		{
			return method.GetCustomAttributes(true)
						.Select(attr => GetHarmonyMethodInfo(attr))
						.Where(info => info != null)
						.ToList();
		}

		/// <summary>Gets all annotations on a method in merged form</summary>
		/// <param name="method">The method</param>
		/// <returns>The merged HarmonyMethod</returns>
		///
		public static HarmonyMethod GetMergedFromMethod(MethodBase method)
		{
			return HarmonyMethod.Merge(GetFromMethod(method));
		}
	}
}