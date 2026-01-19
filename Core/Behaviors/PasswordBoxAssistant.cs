using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace AstroValleyAssistant.Core.Behaviors
{
    public static class PasswordBoxAssistant
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxAssistant),
                new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordBoxAssistant),
                new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordBoxAssistant),
                new PropertyMetadata(false));

        public static void SetBindPassword(DependencyObject dp, bool value) => dp.SetValue(BindPasswordProperty, value);
        public static bool GetBindPassword(DependencyObject dp) => (bool)dp.GetValue(BindPasswordProperty);

        public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPasswordProperty);
        public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPasswordProperty, value);

        private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            if (dp is PasswordBox box)
            {
                if ((bool)e.NewValue)
                {
                    box.PasswordChanged += HandlePasswordChanged;
                }
                else
                {
                    box.PasswordChanged -= HandlePasswordChanged;
                }
            }
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox box = sender as PasswordBox;
            SetUpdatingPassword(box, true);
            SetBoundPassword(box, box.Password);
            SetUpdatingPassword(box, false);
        }

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PasswordBox box = d as PasswordBox;
            if (box != null && (bool)GetUpdatingPassword(box) == false)
            {
                box.Password = (string)e.NewValue;
            }
        }

        private static void SetUpdatingPassword(DependencyObject dp, bool value) => dp.SetValue(UpdatingPasswordProperty, value);
        private static object GetUpdatingPassword(DependencyObject dp) => dp.GetValue(UpdatingPasswordProperty);
    }
}