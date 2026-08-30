// <copyright file="SingleOptionSearchBox.xaml.cs" company="MaaAssistantArknights">
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

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MaaWpfGui.Models;

namespace MaaWpfGui.Styles.Controls;

/// <summary>
/// 单选项搜索选择器：输入即过滤 + 可选中 + 失焦/回车/▼提交。
/// 行为规格见 work-docs/设计文档/单选项搜索选择器-交互与状态机设计.md。
/// 组件不含具体业务逻辑；选项由 <see cref="Options"/> 注入，值由 <see cref="Value"/> 双向写出。
/// </summary>
public partial class SingleOptionSearchBox : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(SingleOptionSearchBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options),
        typeof(IEnumerable<Option>),
        typeof(SingleOptionSearchBox),
        new PropertyMetadata(null, OnOptionsChanged));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder),
        typeof(string),
        typeof(SingleOptionSearchBox),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ErrorTextProperty = DependencyProperty.Register(
        nameof(ErrorText),
        typeof(string),
        typeof(SingleOptionSearchBox),
        new PropertyMetadata(string.Empty));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IEnumerable<Option>? Options
    {
        get => (IEnumerable<Option>?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string ErrorText
    {
        get => (string)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    private bool _isInputMode;        // 输入态（弹层展开）
    private string? _draft;           // 失败草稿（提交失败后保留）
    private bool _hasError;           // 非输入态失败标记
    private bool _isImeComposing;     // IME 组合中
    private List<Option>? _filtered;  // 当前过滤结果（供键盘导航）

    public SingleOptionSearchBox()
    {
        InitializeComponent();

        InputTextBox.AddHandler(TextCompositionManager.PreviewTextInputStartEvent, new RoutedEventHandler(OnTextInputStart), true);
        InputTextBox.AddHandler(TextCompositionManager.TextInputEvent, new RoutedEventHandler(OnTextInput), true);

        IsEnabledChanged += OnIsEnabledChanged;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (SingleOptionSearchBox)d;
        if (!box._isInputMode)
        {
            // 外部回写（如启动加载配置）：仅非输入态同步显示，不覆盖输入中的草稿
            box.InputTextBox.Text = e.NewValue as string ?? string.Empty;
            box._hasError = false;
            box.UpdateErrorVisual();
            box.UpdateWatermark();
        }
    }

    private static void OnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (SingleOptionSearchBox)d;
        if (box._isInputMode)
        {
            box.RefreshPopupContent();
        }
    }

    // ---------- 事件：状态机 E1-E8 ----------

    private void OnTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => EnterInputMode(); // E1

    private void OnTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitAndExit(); // E8

    private void OnToggleButtonClick(object sender, RoutedEventArgs e)
    {
        if (_isInputMode)
        {
            CommitAndExit(); // E3：▼ 点击 = 提交
        }
        else
        {
            InputTextBox.Focus(); // E1：▶ 点击 = 聚焦进入输入态（GotKeyboardFocus 会触发 EnterInputMode）
        }
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsEnabled && _isInputMode)
        {
            CommitAndExit(); // 禁用瞬间退出输入态（语义同失焦）
        }
    }

    private void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            if (e.ImeProcessedKey != Key.None || _isImeComposing)
            {
                return; // E4：IME 组合中，交给输入法选词，不提交
            }

            e.Handled = true;
            if (ResultsListBox.SelectedItem is Option selected)
            {
                CommitAndExit(selected); // E5：有高亮项 → 选中并提交
            }
            else
            {
                CommitAndExit(); // E6：无高亮 → 按当前文本校验
            }

            return;
        }

        if (e.Key is Key.Escape)
        {
            // §4.3 L97：输入态 Esc = ▼（提交 + 退出，忽略高亮项）；IME 组合中交给输入法取消组合；非输入态不处理
            if (_isInputMode && !_isImeComposing && e.ImeProcessedKey == Key.None)
            {
                e.Handled = true;
                CommitAndExit();
            }

            return;
        }

        if (e.Key is Key.Down)
        {
            MoveSelection(1);
            e.Handled = true;
        }
        else if (e.Key is Key.Up)
        {
            MoveSelection(-1);
            e.Handled = true;
        }
    }

    private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInputMode && !_isImeComposing)
        {
            RefreshPopupContent(); // E2：仅刷新下拉，不校验
        }

        UpdateWatermark();
    }

    private void OnResultsListBoxPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        // 隧道阶段沿视觉树找点击项所在的 ListBoxItem（不依赖 SelectedItem 时机）
        var item = FindVisualAncestor<ListBoxItem>(source);
        if (item?.DataContext is Option option)
        {
            e.Handled = true;
            CommitAndExit(option); // E7：点选 = 恒合法提交
        }
    }

    private static T? FindVisualAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null and not T)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        return current as T;
    }

    // ---------- IME ----------

    private void OnTextInputStart(object sender, RoutedEventArgs e) => _isImeComposing = true;

    private void OnTextInput(object sender, RoutedEventArgs e)
    {
        _isImeComposing = false;
        if (_isInputMode)
        {
            RefreshPopupContent();
        }
    }

    // ---------- 内部逻辑 ----------

    private void EnterInputMode()
    {
        if (_isInputMode)
        {
            return;
        }

        _isInputMode = true;
        _hasError = false; // A11：重进输入态清除错误标记
        UpdateErrorVisual();
        InputTextBox.Text = _draft ?? Value; // 草稿保留，否则显示已提交值
        InputTextBox.CaretIndex = InputTextBox.Text.Length;
        UpdateGlyph();
        RefreshPopupContent();
        DropdownPopup.IsOpen = true;
    }

    private void RefreshPopupContent()
    {
        var options = Options?.ToList() ?? [];
        var text = InputTextBox.Text;

        List<Option> filtered;
        if (string.IsNullOrEmpty(text))
        {
            filtered = options.Where(o => o.IsStart).ToList(); // 空 → 推荐子列表（is_start）
        }
        else
        {
            filtered = options.Where(o => o.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList(); // 非空 → Name 子串过滤
        }

        _filtered = filtered;
        ResultsListBox.ItemsSource = filtered;
        ResultsListBox.SelectedIndex = -1; // 无自动高亮

        EmptyResultTextBlock.Visibility = !string.IsNullOrEmpty(text) && filtered.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void MoveSelection(int delta)
    {
        if (_filtered is null || _filtered.Count == 0)
        {
            return;
        }

        var index = ResultsListBox.SelectedIndex;
        index = index < 0
            ? (delta > 0 ? 0 : _filtered.Count - 1)
            : index + delta;
        index = Math.Clamp(index, 0, _filtered.Count - 1);

        ResultsListBox.SelectedIndex = index;
        ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
    }

    private void CommitAndExit(Option? explicitItem = null)
    {
        if (!_isInputMode)
        {
            return; // 幂等：E7 提交后 E8 再次触发不重复提交
        }

        DropdownPopup.IsOpen = false;
        _isInputMode = false;
        UpdateGlyph();

        if (explicitItem != null)
        {
            // E7：点选恒合法
            _draft = null;
            _hasError = false;
            SetValueText(explicitItem.Name);
        }
        else
        {
            var text = InputTextBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                // 空文本提交 = 未选择
                _draft = null;
                _hasError = false;
                SetValueText(string.Empty);
            }
            else if (FindExactMatches(text) is { Count: 1 } single)
            {
                // 单一精确匹配 → 写回 Name
                _draft = null;
                _hasError = false;
                SetValueText(single[0].Name);
            }
            else
            {
                // 0 或 2+ 匹配 → 失败：保留红字草稿，不写 Value
                _draft = text;
                _hasError = true;
                InputTextBox.Text = text;
            }
        }

        UpdateErrorVisual();
        UpdateWatermark();

        // §4.3 五件套第 4 步：退出输入态，控件放弃键盘焦点。
        // 仅当焦点仍在输入框时主动清除（▼/回车/点选路径）；
        // 失焦（E8）路径焦点已离开，不再清，避免吞掉正移向目标控件的焦点（点外部需点两次的问题）。
        if (Keyboard.FocusedElement == InputTextBox)
        {
            Keyboard.ClearFocus();
        }
    }

    private List<Option> FindExactMatches(string text)
    {
        var result = new List<Option>();
        foreach (var option in Options ?? [])
        {
            if (string.Equals(option.Name, text, StringComparison.OrdinalIgnoreCase) ||
                option.ServerNames.Any(s => string.Equals(s, text, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(option);
            }
        }

        return result;
    }

    private void SetValueText(string value)
    {
        Value = value; // TwoWay 写回 ViewModel
        InputTextBox.Text = value;
    }

    private void UpdateErrorVisual()
    {
        var errorBrush = (Brush?)TryFindResource("DangerBrush");
        var primaryBrush = (Brush?)TryFindResource("PrimaryTextBrush");

        if (_hasError)
        {
            BoxBorder.BorderBrush = errorBrush; // 本地值优先于 Style 触发器（错误时边框恒红）
        }
        else
        {
            BoxBorder.ClearValue(Border.BorderBrushProperty); // 交回 Style 触发器（默认灰 / hover 深灰 / 聚焦蓝）
        }

        InputTextBox.Foreground = _hasError ? errorBrush : primaryBrush;
        ErrorHintTextBlock.Visibility = _hasError ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWatermark()
    {
        WatermarkTextBlock.Visibility = !_isInputMode && string.IsNullOrEmpty(InputTextBox.Text) && !_hasError
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateGlyph()
    {
        TogglePath.Data = _isInputMode
            ? (Geometry)TryFindResource("UpGeometry")!
            : (Geometry)TryFindResource("DownGeometry")!;
    }
}
