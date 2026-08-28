// <copyright file="Option.cs" company="MaaAssistantArknights">
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
using System.Collections.Generic;

namespace MaaWpfGui.Models;

/// <summary>
/// 单选项搜索选择器（<see cref="MaaWpfGui.Styles.Controls.SingleOptionSearchBox"/>）的选项。
/// 由调用方（ViewModel）构造注入，组件不含具体业务逻辑。
/// </summary>
public class Option
{
    /// <summary>
    /// Gets 展示名；也是精确匹配的主名与写回模型的值（当前语言下的干员名）。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether 该项进入推荐子列表。
    /// </summary>
    public bool IsStart { get; init; }

    /// <summary>
    /// Gets 其余可用服的官方名（tw/en/jp/kr，unavailable 除外），用于跨服名精确匹配。
    /// </summary>
    public IReadOnlyList<string> ServerNames { get; init; } = [];
}
