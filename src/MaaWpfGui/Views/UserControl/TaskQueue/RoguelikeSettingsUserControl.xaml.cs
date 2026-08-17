// <copyright file="RoguelikeSettingsUserControl.xaml.cs" company="MaaAssistantArknights">
// Part of the MaaWpfGui project, maintained by the MaaAssistantArknights team (Maa Team)
// Copyright (C) 2021-2025 MaaAssistantArknights Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.0 only as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY
// </copyright>

#pragma warning disable SA1402

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using MaaWpfGui.Helper;
using MaaWpfGui.ViewModels.UserControl.TaskQueue;

namespace MaaWpfGui.Views.UserControl.TaskQueue;

/// <summary>
/// RoguelikeSettingsUserControl.xaml 的交互逻辑
/// </summary>
public partial class RoguelikeSettingsUserControl : System.Windows.Controls.UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoguelikeSettingsUserControl"/> class.
    /// </summary>
    public RoguelikeSettingsUserControl()
    {
        InitializeComponent();
    }

    private void StartingCoreCharComboBox_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox || Validation.GetHasError(comboBox))
        {
            return;
        }

        var name = comboBox.Text;
        SetItemsSource(comboBox, RoguelikeSettingsUserControlModel.Instance.RoguelikeCoreCharList);
        comboBox.Text = name;
    }

    private void OnStartingCoreCharValidationError(object sender, ValidationErrorEventArgs e)
    {
        if (e.Action != ValidationErrorEventAction.Added ||
            sender is not ComboBox comboBox ||
            !Validation.GetHasError(comboBox))
        {
            return;
        }

        comboBox.Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (!Validation.GetHasError(comboBox))
                {
                    return;
                }

                var name = comboBox.Text;
                SetItemsSource(comboBox, DataHelper.CharacterNames);
                if (comboBox.Text != name)
                {
                    comboBox.Text = name;
                }
            }),
            DispatcherPriority.Background);
    }

    private static void SetItemsSource(ComboBox comboBox, IEnumerable<string> source)
    {
        if (comboBox.ItemsSource is CollectionView currentView &&
            ReferenceEquals(currentView.SourceCollection, source))
        {
            return;
        }

        var view = new CollectionViewSource { Source = source };
        comboBox.ItemsSource = view.View;
        comboBox.Items.Filter = null;
        comboBox.Items.IsLiveFiltering = true;
    }
}

public class StartingCoreCharRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value is not string stringValue)
        {
            return new ValidationResult(false, HandyControl.Properties.Langs.Lang.FormatError);
        }

        if (!string.IsNullOrEmpty(stringValue) && DataHelper.GetCharacterByNameOrAlias(stringValue) is null)
        {
            return new ValidationResult(false, LocalizationHelper.GetString("RoguelikeStartingCoreCharNotFound"));
        }

        return ValidationResult.ValidResult;
    }
}
