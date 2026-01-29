# NoFences
###kiro
Didn't want to pay 11€, made my own.

![Screenshot](screenshot.png "NoFences in action")

## 构建说明

### 环境要求
- .NET 6.0 SDK
- Windows 10/11

### 构建步骤
1. 克隆仓库
2. 打开命令行，进入项目根目录
3. 运行以下命令构建项目：
   ```bash
   dotnet build -c Release
   ```

### 生成独立exe
运行以下命令生成独立的exe文件：
```bash
   dotnet publish -c Release -r win-x64 --self-contained true
```

生成的exe文件位于 `bin\Release\net6.0-windows\win-x64\publish\` 目录下。

## 功能说明

1. **高清显示**：支持各种屏幕缩放比例，始终保持清晰显示
2. **图标拖动复制**：将图标拖动到栅栏中时，会自动创建快捷方式并保存到独立目录
3. **独立exe**：可以生成单个可执行文件，方便使用

## 项目结构

- **Model**：数据模型，包括栅栏信息和条目信息
- **Util**：工具类，包括扩展方法和缩略图提供者
- **Win32**：Windows API封装，包括桌面操作和窗口管理
- **FenceWindow**：栅栏窗口的实现
- **EditDialog**：编辑栅栏的对话框
- **HeightDialog**：调整栅栏高度的对话框

## 使用说明

1. 运行NoFences.exe
2. 右键点击桌面，选择"新建栅栏"创建一个新的栅栏
3. 将桌面图标拖动到栅栏中，会自动创建快捷方式
4. 可以拖动栅栏边缘调整大小，拖动标题栏移动栅栏
5. 右键点击栅栏可以锁定、解锁或删除栅栏