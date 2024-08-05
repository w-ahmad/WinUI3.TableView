using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;

namespace SampleApp.Helpers;

public static class VisualTreeHelperExtensions
{
    public static T? GetFirstDescendantOfType<T>(this DependencyObject start) where T : DependencyObject
    {
        return start.GetDescendantsOfType<T>().FirstOrDefault();
    }

    public static IEnumerable<T> GetDescendantsOfType<T>(this DependencyObject start) where T : DependencyObject
    {
        return start.GetDescendants().OfType<T>();
    }

    public static IEnumerable<DependencyObject> GetDescendants(this DependencyObject start)
    {
        var queue = new Queue<DependencyObject>();
        var count = VisualTreeHelper.GetChildrenCount(start);

        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(start, i);
            yield return child;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            var count2 = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count2; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                queue.Enqueue(child);
            }
        }
    }

    public static T? GetFirstAncestorOfType<T>(this DependencyObject start) where T : DependencyObject
    {
        return start.GetAncestorsOfType<T>().FirstOrDefault();
    }

    public static IEnumerable<T> GetAncestorsOfType<T>(this DependencyObject start) where T : DependencyObject
    {
        return start.GetAncestors().OfType<T>();
    }

    public static IEnumerable<DependencyObject> GetAncestors(this DependencyObject start)
    {
        var parent = VisualTreeHelper.GetParent(start);

        while (parent != null)
        {
            yield return parent;
            parent = VisualTreeHelper.GetParent(parent);
        }
    }

    public static bool IsInVisualTree(this DependencyObject dob)
    {
        return Window.Current.Content != null && dob.GetAncestors().Contains(Window.Current.Content);
    }

    public static Rect GetBoundingRect(this FrameworkElement dob, FrameworkElement? relativeTo = null)
    {
        if (relativeTo == null)
        {
            relativeTo = Window.Current.Content as FrameworkElement;
        }

        if (relativeTo == null)
        {
            throw new InvalidOperationException("Element not in visual tree.");
        }

        if (dob == relativeTo)
            return new Rect(0, 0, relativeTo.ActualWidth, relativeTo.ActualHeight);

        var ancestors = dob.GetAncestors().ToArray();

        if (!ancestors.Contains(relativeTo))
        {
            throw new InvalidOperationException("Element not in visual tree.");
        }

        var pos =
            dob
                .TransformToVisual(relativeTo)
                .TransformPoint(new Point());
        var pos2 =
            dob
                .TransformToVisual(relativeTo)
                .TransformPoint(
                    new Point(
                        dob.ActualWidth,
                        dob.ActualHeight));

        return new Rect(pos, pos2);
    }

    public static IEnumerable<Type?> GetHierarchyFromUIElement(this Type element)
    {
        if (element.GetTypeInfo().IsSubclassOf(typeof(UIElement)) != true)
        {
            yield break;
        }

        Type? current = element;

        while (current != null && current != typeof(UIElement))
        {
            yield return current;
            current = current.GetTypeInfo().BaseType;
        }
    }

    public static TypeInfo GetTypeInfo(this Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (type is IReflectableType reflectableType)
            return reflectableType.GetTypeInfo();

        return new TypeDelegator(type);
    }

    public static T? FindVisualChildByType<T>(this DependencyObject element) where T : DependencyObject
    {
        if (element == null)
            return null;

        if (element is T elementAsT)
            return elementAsT;

        int childrenCount = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childrenCount; i++)
        {
            var result = VisualTreeHelper.GetChild(element, i).FindVisualChildByType<T>();
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    public static FrameworkElement? FindVisualChildByName(this DependencyObject element, string name)
    {
        if (element == null || string.IsNullOrWhiteSpace(name))
            return null;

        if (element is FrameworkElement elementAsFE && elementAsFE.Name == name)
            return elementAsFE;

        int childrenCount = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childrenCount; i++)
        {
            var result = VisualTreeHelper.GetChild(element, i).FindVisualChildByName(name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    public static T? FindVisualParentByType<T>(this DependencyObject element) where T : DependencyObject
    {
        if (element is null)
            return null;

        return element is T elementAsT
            ? elementAsT
            : VisualTreeHelper.GetParent(element).FindVisualParentByType<T>();
    }

    public static FrameworkElement? FindVisualParentByName(this DependencyObject element, string name)
    {
        if (element is null || string.IsNullOrWhiteSpace(name))
            return null;

        if (element is FrameworkElement elementAsFE && elementAsFE.Name == name)
            return elementAsFE;

        return VisualTreeHelper.GetParent(element).FindVisualParentByName(name);
    }

    /// <summary>
    /// foreach (UIElement element in navigationViewItem.GetChildren()) { _ = results.Add(element); }
    /// </summary>
    public static IEnumerable<UIElement> GetChildren(this UIElement parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            if (VisualTreeHelper.GetChild(parent, i) is UIElement child)
            {
                yield return child;
            }
        }
    }
}
