﻿Imports System.Reflection
Imports System.Threading
Imports SystemRes

Partial Public Class FormGroupSelect
    Inherits Form

    Private Const defaultTitle As String = "选择显示用户组 - Nyaruko Windows User Manager"
    Private version As String = " v"
    Private systemGroups As String()
    Private iniconf As INI
    Private listBoxSystemGroupStartItems As String() = Nothing
    Private listBoxSelectedGroupStartItems As String() = Nothing
    Private closeNow As Boolean = False
    Private closeNO As Boolean = False
    Private slboot As New SlbootAni()
    Private timerStopWaitAniEndMode As Integer = 0
    Private draggingOpacity As DraggingOpacity = Nothing
    Private mainWinPS As String = ""
    Private imgLeft As Bitmap
    Private imgDel As Bitmap
    Private nowAction As String = ""
    Private nowMachine As String = ""

    ''' <summary>
    ''' 表单群组选择 (FormGroupSelect) 的构造函数。
    ''' 初始化表单组件，并根据提供的参数设置相关组件。
    ''' </summary>
    ''' <param name="linkMachineName">可选参数，链接的机器名称。如果未提供，将使用默认值。</param>
    Public Sub New(Optional linkMachineName As String = "")
        InitializeComponent()
        Dim assembly As Assembly = Assembly.GetExecutingAssembly()
        version &= assembly.GetName().Version.ToString()
        Dim versionParts As String() = version.ToString().Split("."c)
        version = String.Join(".", versionParts.Take(versionParts.Length - 1))
        draggingOpacity = New DraggingOpacity(Me)
        ' 獲取命令列引數
        Dim args As String() = Environment.GetCommandLineArgs()
        ' 顯示引數，用於除錯
        'For i As Integer = 0 To args.Length - 1
        '    Console.WriteLine("参数 " & i.ToString() & ": " & args(i))
        'Next

        ' 传入计算机名
        If args.Length > 1 Then
            toolStripComboBoxMachine.Text = args(1)
        End If
        ' 如果提供了链接的机器名称，则设置至类属性。
        If linkMachineName.Length > 0 Then
            toolStripComboBoxMachine.Text = linkMachineName
        End If
        If toolStripComboBoxMachine.Text.Length = 0 Then
            toolStripComboBoxMachine.Text = Environment.MachineName
        End If
        Text = toolStripComboBoxMachine.Text + " 中的用户组 - " + defaultTitle + version

        ' 传入窗口坐标
        If args.Length > 2 Then
            Me.mainWinPS = args(2)
        End If

        ' 初始化 INI 配置对象，读取应用程序启动路径下的 config.ini 文件。
        iniconf = New INI(Application.StartupPath & "\config.ini")

        ' 获取按钮 "Add" 的影像，并翻转以供 "Remove" 按钮使用。
        ' 使用原始 "Add" 按钮的影像，创建翻转后的影像对象。
        imgLeft = New Bitmap(buttonAdd.Image)

        ' 将影像左右翻转。
        imgLeft.RotateFlip(RotateFlipType.RotateNoneFlipX)

        ' 设置 "Remove" 按钮的影像为翻转后的影像。
        buttonRemove.Image = imgLeft

        ' 读取资源文件中的 full_trash
        imgDel = My.Resources.full_trash
    End Sub

    ''' <summary>
    ''' 重写的 WndProc 方法，用于处理 Windows 消息。
    ''' 此方法专注于处理窗口拖动和状态变更时的透明度。
    ''' </summary>
    ''' <param name="m">表示 Windows 消息的 Message 结构。</param>
    Protected Overrides Sub WndProc(ByRef m As Message)
        draggingOpacity.wndProc(m)
        ' 调用基类的 WndProc 方法以处理其余的消息。
        MyBase.WndProc(m)
    End Sub

    ''' <summary>
    ''' 处理取消按钮的点击事件。
    ''' 将对话框结果设置为取消，并关闭当前窗口。
    ''' </summary>
    Private Sub buttonCancel_Click(sender As Object, e As EventArgs) Handles buttonCancel.Click
        ' 设置对话框结果为取消
        DialogResult = DialogResult.Cancel
        ' 标记窗口需要立即关闭
        closeNow = True
        ' 关闭当前窗口
        Close()
    End Sub

    ''' <summary>
    ''' 處理確定按鈕的點選事件。
    ''' 執行虛擬組的檢查和儲存配置，並根據結果決定是否關閉視窗。
    ''' </summary>
    ''' <param name="sender">事件的傳送者物件。</param>
    ''' <param name="e">事件引數。</param>
    Private Sub buttonOK_Click(sender As Object, e As EventArgs) Handles buttonOK.Click
        ' 保存配置并检查保存是否成功
        If saveConfig() Then
            If Not closeNO Then
                ' 设置对话框结果为确定
                DialogResult = DialogResult.OK
                ' 如果事件不是窗口关闭事件，关闭当前窗口
                If Not TypeOf e Is FormClosingEventArgs Then
                    closeNow = True
                    Environment.Exit(2)
                    Close()
                End If
            End If
        Else
            Environment.Exit(1)
        End If
    End Sub

    ''' <summary>
    ''' 表单加载事件，用于初始化组件和启动获取用户组的线程。
    ''' </summary>
    Private Sub FormGroupSelect_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' 處理視窗座標
        If Me.mainWinPS.Length > 0 Then
            Dim mainWinPS As String() = Me.mainWinPS.Split(","c)
            Dim mainWinPSn As Integer() = Array.ConvertAll(mainWinPS, Function(str) Convert.ToInt32(str))
            If mainWinPSn.Length = 4 Then
                Me.Location = New Point(mainWinPSn(0), mainWinPSn(1))
                'Me.Size = New Size(mainWinPSn(2), mainWinPSn(3))
                Opacity = 1
            End If
        End If

        ' 初始化動畫設定，指定引數 20
        slboot.Init(20)
        ' 如果動畫字型不為空，設定等待標籤的字型
        If slboot.aniFont IsNot Nothing Then
            labelWait.Font = slboot.aniFont
        End If

        reloadConfig()
    End Sub

    ''' <summary>
    ''' 處理新增按鈕的點選事件，將選中的專案從系統組列表框移動到已選組列表框。
    ''' </summary>
    ''' <param name="sender">事件的觸發者</param>
    ''' <param name="e">事件引數</param>
    Private Sub buttonAdd_Click(sender As Object, e As EventArgs) Handles buttonAdd.Click
        ' 如果未選擇任何專案，直接返回
        If listBoxSystemGroup.SelectedIndex = -1 Then
            Return
        End If

        ' 儲存當前選中項的索引
        Dim nowSelectedIndex As Integer = listBoxSystemGroup.SelectedIndex
        ' 獲取選中的所有專案，並將其轉換為列表
        Dim selectedObjectCollection As List(Of Object) = listBoxSystemGroup.SelectedItems.Cast(Of Object)().ToList()

        ' 清除目標列表框的選中狀態
        listBoxSelectedGroup.SelectedItems.Clear()

        ' 將選中的專案從系統組列表框移動到已選組列表框
        For Each item In selectedObjectCollection
            listBoxSelectedGroup.Items.Add(item) ' 新增到已選組列表框
            listBoxSelectedGroup.SelectedItems.Add(item) ' 設定為選中狀態
            listBoxSystemGroup.Items.Remove(item) ' 從系統組列表框移除
        Next

        ' 更新系統組列表框的選中狀態
        If listBoxSystemGroup.Items.Count > 0 AndAlso nowSelectedIndex >= 0 Then
            If nowSelectedIndex >= listBoxSystemGroup.Items.Count Then
                listBoxSystemGroup.SelectedIndex = listBoxSystemGroup.Items.Count - 1
            Else
                listBoxSystemGroup.SelectedIndex = nowSelectedIndex
            End If
        End If

        ' 檢查按鈕狀態
        chkBtnEnable()
        ' 檢查狀態改變
        chkChange()
    End Sub

    ''' <summary>
    ''' 處理系統組列表框的選中項更改事件。
    ''' 用於更新按鈕的啟用狀態。
    ''' </summary>
    Private Sub listBoxSystemGroup_SelectedIndexChanged(sender As Object, e As EventArgs) Handles listBoxSystemGroup.SelectedIndexChanged
        chkBtnEnable()
    End Sub

    ''' <summary>
    ''' 檢查並更新新增和移除按鈕的啟用狀態。
    ''' 根據當前選中的項，調整按鈕狀態並切換按鈕影像。
    ''' </summary>
    ''' <param name="sender">事件的傳送者物件，通常是觸發事件的控制元件。</param>
    ''' <param name="e">包含事件資料的 EventArgs 物件。</param>
    Private Sub listBoxSelectedGroup_SelectedIndexChanged(sender As Object, e As EventArgs) Handles listBoxSelectedGroup.SelectedIndexChanged
        ' 呼叫 chkBtnEnable 方法以啟用或停用相關按鈕
        chkBtnEnable()

        ' 檢查當前選中的項是否滿足某種條件（透過 itemIsV 方法判斷）
        If itemIsV() Then
            ' 如果條件滿足，檢查按鈕影像是否為 imgDel，如果不是則更新為 imgDel
            If Not buttonRemove.Image.Equals(Me.imgDel) Then
                buttonRemove.Image = Me.imgDel
            End If
        Else
            ' 如果條件不滿足，檢查按鈕影像是否為 imgLeft，如果不是則更新為 imgLeft
            If Not buttonRemove.Image.Equals(Me.imgLeft) Then
                buttonRemove.Image = Me.imgLeft
            End If
        End If
    End Sub

    ''' <summary>
    ''' 處理移除按鈕的點選事件，將選中的專案從已選組列表框移動到系統組列表框。
    ''' </summary>
    Private Sub buttonRemove_Click(sender As Object, e As EventArgs) Handles buttonRemove.Click
        ' 儲存當前選中項的索引
        Dim nowSelectedIndex As Integer = listBoxSelectedGroup.SelectedIndex
        ' 如果未選擇任何項，直接返回
        If nowSelectedIndex = -1 Then
            buttonRemove.Enabled = False
            Return
        End If

        ' 判斷這個是不是不在系統中存在的使用者組（虛擬使用者組）
        If itemIsV() Then
            If MessageBox.Show(listBoxSelectedGroup.SelectedItem.ToString() & Environment.NewLine & "是一个不实际存在于系统中的用户组，" & Environment.NewLine &
                   "因此此操作会将其永久删除。" & Environment.NewLine & "确认吗？",
                   "删除虚拟用户组",
                   MessageBoxButtons.OKCancel,
                   MessageBoxIcon.Question) = DialogResult.OK Then
                listBoxSelectedGroup.Items.Remove(listBoxSelectedGroup.SelectedItem)
            End If
        Else
            ' 獲取選中的所有項，並將其轉換為列表
            Dim selectedObjectCollection As List(Of Object) = listBoxSelectedGroup.SelectedItems.Cast(Of Object)().ToList()

            ' 清除目標列表框的選中狀態
            listBoxSystemGroup.SelectedItems.Clear()

            ' 將選中的專案從已選組列表框移動到系統組列表框
            For Each item As Object In selectedObjectCollection
                listBoxSystemGroup.Items.Add(item) ' 新增到系統組列表框
                listBoxSystemGroup.SelectedItems.Add(item) ' 設定為選中狀態
                listBoxSelectedGroup.Items.Remove(item) ' 從已選組列表框移除
            Next
        End If

        ' 更新已選組列表框的選中狀態
        If listBoxSelectedGroup.Items.Count > 0 AndAlso nowSelectedIndex >= 0 Then
            If nowSelectedIndex >= listBoxSelectedGroup.Items.Count Then
                listBoxSelectedGroup.SelectedIndex = listBoxSelectedGroup.Items.Count - 1
            Else
                listBoxSelectedGroup.SelectedIndex = nowSelectedIndex
            End If
        End If

        ' 檢查按鈕狀態
        chkBtnEnable()
        ' 檢查狀態改變
        chkChange()
    End Sub

    ''' <summary>
    ''' 當文字框內容發生變化時觸發此事件處理程式。
    ''' 根據文字框的內容長度，啟用或禁用新增自定義按鈕。
    ''' </summary>
    ''' <param name="sender">事件的傳送者物件</param>
    ''' <param name="e">事件引數</param>
    Private Sub textBoxCustom_TextChanged(sender As Object, e As EventArgs) Handles textBoxCustom.TextChanged
        ' 如果文字框有內容，則啟用按鈕，否則禁用按鈕
        buttonAddCustom.Enabled = textBoxCustom.Text.Length > 0
        buttonAddGroup.Enabled = textBoxCustom.Text.Length > 0
    End Sub

    ''' <summary>
    ''' 當用戶在自定義文字框中輸入內容時觸發此事件處理程式。
    ''' 僅允許輸入字母、數字、連字元、下劃線和空格，阻止其他字元輸入。
    ''' </summary>
    ''' <param name="sender">事件的傳送者物件</param>
    ''' <param name="e">按鍵事件引數</param>
    Private Sub textBoxCustom_KeyPress(sender As Object, e As KeyPressEventArgs)
        ' 檢查按鍵是否符合以下條件：
        ' 1. 按鍵不是控制字元（如退格鍵、刪除鍵等）
        ' 2. 按鍵不是字母或數字
        ' 3. 按鍵不是連字元（-）
        ' 4. 按鍵不是下劃線（_）
        ' 5. 按鍵不是空格
        ' 如果上述條件都成立，則禁止輸入。
        If Not Char.IsControl(e.KeyChar) AndAlso
       Not Char.IsLetterOrDigit(e.KeyChar) AndAlso
       e.KeyChar <> "-"c AndAlso
       e.KeyChar <> "_"c AndAlso
       e.KeyChar <> " "c Then
            ' 設定 e.Handled 為 True 表示此按鍵事件已處理，按鍵輸入將被忽略。
            e.Handled = True
        End If
    End Sub

    ''' <summary>
    ''' 當點選“新增自定義”按鈕時觸發此事件處理程式。
    ''' 將文字框內容新增到選定組列表框，並清空文字框，處理重複項和按鈕狀態。
    ''' </summary>
    ''' <param name="sender">事件的傳送者物件</param>
    ''' <param name="e">事件引數</param>
    Private Sub buttonAddCustom_Click(sender As Object, e As EventArgs) Handles buttonAddCustom.Click
        If textBoxCustom.Text.Length = 0 Then
            Return
        End If
        ' 將文字框內容新增到選定組列表框中
        listBoxSelectedGroup.Items.Add(textBoxCustom.Text)
        ' 清空文字框內容
        textBoxCustom.Text = ""
        ' 移除重複專案
        removeDuplicateItems()
        ' 檢查按鈕是否應啟用
        chkBtnEnable()
        ' 檢查是否有更改
        chkChange()
    End Sub

    ''' <summary>
    ''' 當窗體關閉時觸發此事件處理程式。
    ''' 如果有未儲存的更改，提示使用者是否儲存更改。
    ''' </summary>
    ''' <param name="sender">事件的傳送者物件</param>
    ''' <param name="e">窗體關閉事件引數</param>
    Private Sub MainWindow_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        ' 如果可以直接关闭，则无需进一步处理
        If closeNow Then
            e.Cancel = False
            Return
        End If

        ' 如果有未保存的更改，提示用户选择操作
        Dim result As DialogResult = ChkUnsavedChanges()
        If result = DialogResult.Abort Then
            ' 沒有未儲存的變更
            Return
        ElseIf result = DialogResult.Yes Then
            ' 有未儲存的變更並且儲存
            closeNow = True
            buttonOK_Click(sender, e) ' 调用保存按钮的点击事件
        ElseIf result = DialogResult.No Then
            ' 有未儲存的變更但是不儲存
        ElseIf result = DialogResult.Cancel Then
            ' 有未儲存的變更但是不继续
            e.Cancel = True ' 取消关闭操作
        End If
    End Sub


    ''' <summary>
    ''' 定時器 tick 事件處理器，用於停止等待動畫。
    ''' </summary>
    ''' <param name="sender">事件的傳送者物件。</param>
    ''' <param name="e">事件引數。</param>
    Private Sub timerStopWaitAni_Tick(sender As Object, e As EventArgs) Handles timerStopWaitAni.Tick
        ' 停止等待动画
        waitAni(False)
    End Sub

    ''' <summary>
    ''' 處理主視窗移動事件的方法。
    ''' 當主視窗移動時，通知透明度控制。
    ''' </summary>
    ''' <param name="sender">事件的傳送者（主視窗）</param>
    ''' <param name="e">事件引數。</param>
    Private Sub FormGroupSelect_Move(sender As Object, e As EventArgs) Handles MyBase.Move
        draggingOpacity.winMove()
    End Sub

    ''' <summary>
    ''' 窗體大小改變事件處理程式。
    ''' 當窗體大小發生變化時，防止觸發透明效果。
    ''' </summary>
    ''' <param name="sender">事件的傳送者。</param>
    ''' <param name="e">事件引數。</param>
    Private Sub FormGroupSelect_SizeChanged(sender As Object, e As EventArgs) Handles MyBase.SizeChanged
        If draggingOpacity IsNot
            Nothing Then
            draggingOpacity.firstMove = True
        End If
    End Sub

    ''' <summary>
    ''' 等待動畫計時器的滴答事件處理。
    ''' 更新等待動畫的顯示字元。
    ''' </summary>
    ''' <param name="sender">事件的傳送者物件。</param>
    ''' <param name="e">事件引數。</param>
    Private Sub timerWaitAni_Tick(sender As Object, e As EventArgs) Handles timerWaitAni.Tick
        ' 更新等待動畫標籤中的字元
        labelWait.Text = slboot.UpdateChar()
    End Sub

    ''' <summary>
    ''' 點選“新增組”按鈕時的事件處理程式。
    ''' 如果未選擇機器或未輸入自定義文字框，則停用按鈕並返回。
    ''' 如果當前使用者沒有管理員許可權或未進行使用者操作，則直接返回。
    ''' 建立一個後臺執行緒，用於非同步新增使用者組。
    ''' </summary>
    ''' <param name="sender">觸發事件的控制元件。</param>
    ''' <param name="e">包含事件資料的引數。</param>
    Private Sub buttonAddGroup_Click(sender As Object, e As EventArgs) Handles buttonAddGroup.Click
        ' 檢查工具欄中的機器選擇框是否為空，以及自定義文字框的輸入是否為零
        If toolStripComboBoxMachine.Text.Length = 0 Or textBoxCustom.Text = 0 Then
            ' 停用“新增組”按鈕
            buttonAddGroup.Enabled = False
            ' 提前返回，停止執行
            Return
        End If

        ' 記錄當前選擇的機器名稱
        nowMachine = toolStripComboBoxMachine.Text

        ' 檢查使用者是否具有管理員許可權，並確認是否執行使用者操作
        If Not useAdmin() Or Not userAction() Then
            ' 如果條件不滿足，則直接返回
            Return
        End If

        ' 建立一個後臺執行緒，用於非同步執行新增使用者組的操作
        Dim thread As New Thread(AddressOf addUserGroup)
        thread.IsBackground = True ' 設定執行緒為後臺執行
        thread.Start()
    End Sub


    ''' <summary>
    ''' 重新載入按鈕的點選事件處理函式。
    ''' 根據是否存在未儲存的更改，執行相應的處理邏輯。
    ''' </summary>
    ''' <param name="sender">事件的傳送者。</param>
    ''' <param name="e">包含事件資料的 EventArgs 物件。</param>
    Private Sub bottonReload_Click(sender As Object, e As EventArgs) Handles bottonReload.Click
        ' 檢查是否存在未儲存的更改，返回的結果儲存在 result 中。
        Dim result As DialogResult = ChkUnsavedChanges()

        ' 如果沒有未儲存的更改，則直接重新載入配置。
        If result = DialogResult.Abort Then
            reloadConfig()

            ' 如果有未儲存的更改並選擇了儲存，先儲存更改，然後重新載入配置。
        ElseIf result = DialogResult.Yes Then
            closeNO = True ' 標記不需要關閉。
            buttonOK_Click(sender, e) ' 呼叫確認按鈕的點選事件以儲存更改。
            reloadConfig()

            ' 如果有未儲存的更改並選擇了不儲存，直接重新載入配置。
        ElseIf result = DialogResult.No Then
            reloadConfig()

            ' 如果有未儲存的更改並選擇了取消操作，則退出處理。
        ElseIf result = DialogResult.Cancel Then
            Return
        End If
    End Sub

    ''' <summary>
    ''' 處理刪除使用者組按鈕的點選事件。
    ''' 當用戶確認刪除時，啟動一個後臺執行緒執行刪除操作。
    ''' </summary>
    ''' <param name="sender">觸發事件的控制元件物件。</param>
    ''' <param name="e">事件引數。</param>
    Private Sub buttonDelGroup_Click(sender As Object, e As EventArgs) Handles buttonDelGroup.Click
        ' 檢查是否選中了使用者組
        If listBoxSystemGroup.SelectedIndex < 0 Then
            ' 如果沒有選中使用者組，則停用刪除按鈕並返回
            buttonDelGroup.Enabled = False
            Return
        End If

        ' 獲取當前選中的裝置和操作
        nowMachine = toolStripComboBoxMachine.Text
        nowAction = listBoxSystemGroup.SelectedItem.ToString()

        ' 驗證是否有管理員許可權並允許當前操作
        If Not useAdmin() Or Not userAction() Then
            Return
        End If

        ' 顯示確認刪除使用者組的對話方塊
        If MessageBox.Show(
            "要删除用户组 " & nowAction & " 吗？",
            "删除用户组",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) = DialogResult.Yes Then

            ' 建立一個後臺執行緒以執行刪除使用者組操作
            Dim thread As New Thread(AddressOf delUserGroup)
            thread.IsBackground = True
            thread.Start()
        End If
    End Sub

    Private Sub FormGroupSelect_ResizeEnd(sender As Object, e As EventArgs) Handles MyBase.ResizeEnd
        Opacity = 1
    End Sub
End Class
