﻿Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading
Imports System.Windows.Forms
Imports UserInfo

Partial Public Class FormGroupSelect
    Inherits Form

    Private Sub reloadConfig()
        ' 如果動畫字型不為空，開始等待動畫
        If slboot.aniFont IsNot Nothing Then
            labelWait.Visible = True
            timerWaitAni.Enabled = True
        End If
        listBoxSelectedGroup.Items.Clear()
        listBoxSystemGroup.Items.Clear()
        listBoxSystemGroupStartItems = Nothing
        listBoxSelectedGroupStartItems = Nothing
        ' 建立一個後臺執行緒，用於非同步獲取使用者組
        Dim thread As New Thread(AddressOf getUserGroup) With {
            .IsBackground = True
        }
        thread.Start() ' 啟動執行緒
    End Sub

    ''' <summary>
    ''' 儲存當前配置到 INI 檔案。
    ''' </summary>
    ''' <returns>儲存成功返回 true，失敗返回 false。</returns>
    Private Function saveConfig() As Boolean
        Try
            ' 寫入組數量到 INI 檔案
            iniconf.IniWriteValue("Config", "GroupsCount", listBoxSelectedGroup.Items.Count.ToString())
            ' 寫入每個組名到 INI 檔案
            For i As Integer = 0 To listBoxSelectedGroup.Items.Count - 1
                iniconf.IniWriteValue("Groups", $"G{i}", listBoxSelectedGroup.Items(i).ToString())
            Next
            Return True
        Catch ex As Exception
            ' 彈出錯誤提示並允許使户者试試儲配置
            If MessageBox.Show(ex.Message, "配置写入失败", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) = DialogResult.Retry Then
                Return saveConfig()
            End If
        End Try
        Return False
    End Function

    ''' <summary>
    ''' 從 INI 檔案載入配置。
    ''' </summary>
    Private Sub loadConfig()
        ' 檢查 INI 檔案是否存在
        If Not iniconf.ExistINIFile() Then
            Return
        End If

        Try
            ' 讀取組數量並載入每個組名到列表框
            Dim countS As String = iniconf.IniReadValue("Config", "GroupsCount")
            If countS.Length = 0 Then
                Return
            End If
            Dim count As Integer = Integer.Parse(countS)
            For i As Integer = 0 To count - 1
                listBoxSelectedGroup.Items.Add(iniconf.IniReadValue("Groups", $"G{i}"))
            Next
            ' 移除重複的組名
            removeDuplicateItems()
        Catch ex As Exception
            ' 彈出錯誤提示
            If MessageBox.Show(ex.Message, "配置读取失败", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) = DialogResult.Retry Then
                loadConfig()
            End If
        End Try
    End Sub

    ''' <summary>
    ''' 獲取系統使用者組的方法，執行在單獨的執行緒中。
    ''' 獲取完成後，透過 Invoke 方法更新 UI。
    ''' </summary>
    Private Sub getUserGroup()
        ' 呼叫 UserLoader 獲取系統使用者組資訊
        Dim groups As String()() = UserLoader.GetGroups(linkMachineName)

        ' 使用 Invoke 更新 UI 執行緒上的控制元件
        Me.Invoke(CType(Sub()
                            ' 如果獲取的組列表為空或表示錯誤，則顯示錯誤資訊並關閉視窗
                            If groups.Length = 0 OrElse (groups.Length = 1 AndAlso groups(0).Length >= 1 AndAlso groups(0)(0) = "%E%") Then
                                Dim errinfo As String = "无法获取系统用户组列表"
                                If groups.Length = 1 AndAlso groups(0).Length >= 1 AndAlso groups(0)(0).Length >= 2 Then
                                    errinfo = groups(0)(1) ' 使用错误信息
                                End If
                                MessageBox.Show(errinfo, "无法获取系统用户组列表", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                closeNow = True
                                timerWaitAni.Enabled = False
                                Me.Close()
                                Return
                            End If

                            ' 將獲取的使用者組名稱新增到列表框中
                            For Each group In groups
                                listBoxSystemGroup.Items.Add(group(1))
                            Next

                            ' 將系統使用者組儲存為陣列
                            systemGroups = New String(listBoxSystemGroup.Items.Count - 1) {}
                            listBoxSystemGroup.Items.CopyTo(systemGroups, 0)

                            ' 載入配置
                            loadConfig()

                            ' 儲存初始的列表框狀態
                            listBoxSystemGroupStartItems = listBoxSystemGroup.Items.Cast(Of String)().ToArray()
                            listBoxSelectedGroupStartItems = listBoxSelectedGroup.Items.Cast(Of String)().ToArray()

                            ' 檢查是否發生更改
                            chkChange()

                            ' 啟用停止動畫的計時器
                            timerStopWaitAni.Enabled = True
                        End Sub, Action))
    End Sub

    ''' <summary>
    ''' 檢查使用者組列表框是否有更改，並啟用/禁用確定按鈕。
    ''' </summary>
    ''' <returns>返回所有列表框是否都未更改</returns>
    Private Function chkChange() As Boolean
        ' 獲取當前列表框的專案
        Dim listBoxSystemGroupNowItems As String() = listBoxSystemGroup.Items.Cast(Of String)().ToArray()
        Dim listBoxSelectedGroupNowItems As String() = listBoxSelectedGroup.Items.Cast(Of String)().ToArray()

        ' 比較當前專案和初始專案是否一致
        Dim listBoxSystemGroupEqual As Boolean = listBoxSystemGroupStartItems.SequenceEqual(listBoxSystemGroupNowItems)
        Dim listBoxSelectedGroupEqual As Boolean = listBoxSelectedGroupStartItems.SequenceEqual(listBoxSelectedGroupNowItems)

        ' 檢查所有列表框是否都未更改
        Dim allEqual As Boolean = listBoxSystemGroupEqual AndAlso listBoxSelectedGroupEqual

        ' 根據是否更改設定確定按鈕的啟用狀態
        buttonOK.Enabled = Not allEqual
        Return allEqual
    End Function

    ''' <summary>
    ''' 檢查是否有被移除的虛擬組
    ''' </summary>
    ''' <returns>有返回 true，否則返回 false。</returns>
    Private Function chkIsRemoveVGroup() As Boolean
        Dim removedGroups As String() = chkSystemGroup()
        Return removedGroups.Length > 0
    End Function

    Private Function ChkUnsavedChanges() As DialogResult
        ' 如果有未儲存的更改，提示使用者選擇操作
        If Not chkChange() Then
            Return MessageBox.Show(
                "列的显示配置还没有保存，是否要保存更改？",
                "更改未保存",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question)
        End If
        Return DialogResult.Abort
    End Function

    Private Function itemIsV() As Boolean
        Dim vGroups As String() = chkSystemGroup()
        Dim isV As Boolean = False
        If vGroups.Length > 0 And listBoxSelectedGroup.SelectedIndex > -1 Then
            ' 检查是不是在 removedGroups 中
            For Each item In vGroups
                If item = listBoxSelectedGroup.SelectedItem.ToString() Then
                    isV = True
                    Exit For
                End If
            Next
        End If
        Return isV
    End Function

    ''' <summary>
    ''' 檢查當前系統使用者組列表框中新增的使用者組。
    ''' </summary>
    ''' <returns>返回新增使用者組的字串陣列</returns>
    Private Function chkSystemGroup() As String()
        ' 建立一個列表用於儲存新增的使用者組
        Dim list As New List(Of String)()

        ' 遍歷當前使用者組列表框的
        For Each item In listBoxSystemGroup.Items
            ' 如果該使用者組不在原始組中，則認為是新增的
            If Not systemGroups.Contains(item.ToString()) Then
                list.Add(item.ToString())
            End If
        Next
        ' 遍歷當前系統組列表框
        For Each item In listBoxSelectedGroup.Items
            ' 如果該使用者組不在原始組中，則認為是新增的
            If Not systemGroups.Contains(item.ToString()) Then
                list.Add(item.ToString())
            ElseIf list.Contains(item.ToString()) Then
                ' 如果該使用者組在新增列表中，則移除
                list.Remove(item.ToString())
            End If
        Next

        ' 返回新增使用者組的陣列
        Return list.ToArray()
    End Function

    ''' <summary>
    ''' 檢查並更新新增和移除按鈕的啟用狀態。
    ''' </summary>
    Private Sub chkBtnEnable()
        ' 如果系統組列表框中有選中項，則啟用刪除按鈕
        buttonDelGroup.Enabled = listBoxSystemGroup.SelectedIndex <> -1
        ' 如果已選組列表框中有選中項，則啟用移除按鈕
        buttonRemove.Enabled = listBoxSelectedGroup.SelectedIndex <> -1
        ' 如果系統組列表框中有選中項，則啟用新增按鈕
        buttonAdd.Enabled = listBoxSystemGroup.SelectedIndex <> -1
    End Sub

    ''' <summary>
    ''' 移除在列表框中重複的專案。
    ''' 從 listBoxSelectedGroup 中的專案中查詢，並刪除 listBoxSystemGroup 中的重複項。
    ''' </summary>
    Private Sub removeDuplicateItems()
        ' 遍歷 listBoxSelectedGroup 的所有專案
        For i As Integer = listBoxSelectedGroup.Items.Count - 1 To 0 Step -1
            ' 遍歷 listBoxSystemGroup 的所有專案
            For j As Integer = listBoxSystemGroup.Items.Count - 1 To 0 Step -1
                ' 如果兩個列表中有相同的專案，則從 listBoxSystemGroup 中移除
                If listBoxSelectedGroup.Items(i).ToString() = listBoxSystemGroup.Items(j).ToString() Then
                    listBoxSystemGroup.Items.RemoveAt(j)
                End If
            Next
        Next
    End Sub

    ''' <summary>
    ''' 控制等待動畫的啟用或禁用。
    ''' </summary>
    ''' <param name="enable">是否啟用等待動畫。true 表示啟用，false 表示禁用。</param>
    Private Sub waitAni(enable As Boolean)
        ' 如果禁用動畫並且結束模式為 -1，則直接返回
        If Not enable AndAlso timerStopWaitAniEndMode = -1 Then
            Return
        End If

        ' 如果禁用動畫並且結束模式為 1，則退出應用程式
        If Not enable AndAlso timerStopWaitAniEndMode = 1 Then
            Application.Exit()
        End If

        ' 設定等待游標狀態
        UseWaitCursor = enable

        ' 設定等待標籤的可見性
        labelWait.Visible = enable

        ' 如果啟用動畫並且動畫字型不為空
        If enable AndAlso slboot.aniFont IsNot Nothing Then
            ' 啟用等待動畫和停止等待動畫的定時器
            timerWaitAni.Enabled = enable
            timerStopWaitAni.Enabled = enable
        Else
            ' 禁用定時器並停止更新字元
            timerWaitAni.Enabled = False
            timerStopWaitAni.Enabled = False
            slboot.StopUpdateChar()
        End If
    End Sub

End Class
