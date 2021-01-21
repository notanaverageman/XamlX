using System;
using System.Reflection;
using System.Reflection.Emit;
using XamlX.Ast;
using XamlX.IL;
using XamlX.TypeSystem;

namespace XamlX.IL
{
#if !XAMLX_INTERNAL
    public
#endif
        static class HotReloadExtensions
    {
        public static IDisposable EmitSetPropertyMarker(this IXamlILEmitter emitter, IXamlPropertySetter setter)
        {
            (string type, string property) = GetPropertySetterInfo(setter);

            return emitter.EmitMarker(
                "StartSetPropertyMarker",
                "EndSetPropertyMarker",
                type,
                property);
        }

        public static IDisposable EmitObjectInitializationMarker(this IXamlILEmitter emitter, string type)
        {
            return emitter.EmitMarker(
                "StartObjectInitializationMarker",
                "EndObjectInitializationMarker",
                type);
        }

        public static IDisposable EmitAddChildMarker(
            this IXamlILEmitter emitter,
            string type,
            string property)
        {
            return emitter.EmitMarker("AddChildMarker", null, type, property);
        }

        public static IDisposable EmitContextInitializationMarker(this IXamlILEmitter emitter)
        {
            return emitter.EmitMarker("StartContextInitializationMarker", "EndContextInitializationMarker");
        }

        public static IDisposable EmitNewObjectMarker(this IXamlILEmitter emitter)
        {
            return emitter.EmitMarker("StartNewObjectMarker", "EndNewObjectMarker");
        }

        private static IDisposable EmitMarker(
            this IXamlILEmitter emitter,
            string startMethodName,
            string endMethodName = null,
            params string[] parameters)
        {
            var markers = emitter.TypeSystem.FindType("Avalonia.Markup.Xaml.HotReload.XamlMarkers");

            if (markers == null)
            {
                return EmptyDisposable.Instance;
            }

            var startMarker = markers.FindMethod(m => m.Name == startMethodName);

            foreach (string parameter in parameters)
            {
                emitter.Emit(OpCodes.Ldstr, parameter);
            }

            emitter.EmitCall(startMarker);

            if (endMethodName != null)
            {
                var endMarker = markers.FindMethod(m => m.Name == endMethodName);
                return new ActionDisposable(() => emitter.EmitCall(endMarker));
            }

            return EmptyDisposable.Instance;
        }

        private static (string Type, string Property) GetPropertySetterInfo(IXamlPropertySetter setter)
        {
            // XamlDirectCallPropertySetter
            var directCallPropertySetterMethod = setter
                .GetType()
                .GetField("_method", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(setter) as IXamlMethod;

            if (directCallPropertySetterMethod != null)
            {
                return (setter.TargetType.FullName, directCallPropertySetterMethod.Name.Replace("set_", ""));
            }

            // BindingSetter
            var bindingSetterField = setter
                .GetType()
                .GetField("AvaloniaProperty", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(setter) as IXamlField;

            if (bindingSetterField != null)
            {
                return (setter.TargetType.FullName, bindingSetterField.Name);
            }

            // AdderSetter
            var adderSetterField = setter
                .GetType()
                .GetField("_getter", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(setter) as IXamlMethod;

            if (adderSetterField != null)
            {
                return (setter.TargetType.FullName, adderSetterField.Name);
            }

            return (setter.TargetType.FullName, setter.ToString());
        }

        private class ActionDisposable : IDisposable
        {
            private readonly Action _action;

            public ActionDisposable(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }

        private class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            private EmptyDisposable()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
