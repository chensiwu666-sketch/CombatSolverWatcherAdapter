using System;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Models;

namespace WatcherAdapter;

/// <summary>
/// Registers a handler into a mirror table that is dispatched by the solver but has no public
/// <c>Register</c> method — only a private static <c>Registry</c> field.
/// </summary>
/// <remarks>
/// Several families (AfterDamageReceived, AfterDamageGiven, ModifyBlockMultiplicative, …) keep the
/// registry instance private while the registry class itself exposes <c>Register&lt;TModel&gt;</c>. The field
/// is therefore the cheapest honest way in: no Harmony patch on the dispatch path, no copy of the
/// family's arithmetic, and a loud failure when the solver renames the field.
///
/// A registry that falls through to the model's own method for unregistered types would run mod code
/// on the search thread, so a busted bridge must be visible in the log rather than silently reverting
/// the family to that fall-through.
/// </remarks>
internal static class MirrorBridge
{
    internal static bool TryRegister<TModel, TContext>(
        Type mirrorType,
        string fieldName,
        Action<TModel, TContext> handler,
        out string error)
        where TModel : AbstractModel
    {
        try
        {
            FieldInfo? field = mirrorType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (field is null)
            {
                error = $"{mirrorType.Name}.{fieldName} not found";
                return false;
            }

            object? registry = field.GetValue(null);
            if (registry is null)
            {
                error = $"{mirrorType.Name}.{fieldName} is null";
                return false;
            }

            // The generic Register<TModel>(Action<TModel, TContext>) overload; RegisterIgnored and the
            // result-returning variants have a different parameter count, so one filter selects it.
            MethodInfo? register = registry.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name == "Register"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1);
            if (register is null)
            {
                error = $"{registry.GetType().Name} has no Register<TModel>(handler)";
                return false;
            }

            register.MakeGenericMethod(typeof(TModel)).Invoke(registry, [handler]);
            error = string.Empty;
            return true;
        }
        catch (TargetInvocationException ex)
        {
            error = (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }
}
