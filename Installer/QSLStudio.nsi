# -*- coding: utf-8 -*-
Unicode true
!ifdef QA_PER_USER
    RequestExecutionLevel user
!else
    RequestExecutionLevel admin
!endif
ManifestDPIAware true
CRCCheck force
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!include "Sections.nsh"

!ifndef APP_NAME
    !define APP_NAME "QSL 卡片工坊"
!endif
!ifndef APP_VERSION
    !define APP_VERSION "1.10.5"
!endif
!ifndef REG_NAME
    !define REG_NAME "QSLStudio"
!endif
!define APP_REG "Software\${REG_NAME}"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${REG_NAME}"
!define NET4_KEY "Software\Microsoft\NET Framework Setup\NDP\v4\Full"
!define USER_INSTDIR "$LOCALAPPDATA\Programs\${REG_NAME}"
!define SHORTCUT_ICON "QSLStudio-${APP_VERSION}.ico"
!ifdef QA_PER_USER
    !define QSL_DESKTOP "$LOCALAPPDATA\${REG_NAME}\Shell\Desktop"
    !define QSL_PROGRAMS "$LOCALAPPDATA\${REG_NAME}\Shell\Programs"
!else
    !define QSL_DESKTOP "$DESKTOP"
    !define QSL_PROGRAMS "$SMPROGRAMS"
!endif
Var PreviousInstallDir
Var MigrateFrom
Var ReplacingInstall

Name "${APP_NAME}"
Caption "${APP_NAME} 安装向导"
UninstallCaption "卸载 ${APP_NAME}"
OutFile "${OUTPUT}"
InstallDir "${USER_INSTDIR}"
InstallDirRegKey HKCU "${APP_REG}" "InstallDir"
Icon "${ART}\QSLStudio.ico"
UninstallIcon "${ART}\QSLStudio.ico"
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=2052 "FileDescription" "${APP_NAME}安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "OriginalFilename" "QSLStudio-${APP_VERSION}-Setup-x86.exe"
VIAddVersionKey /LANG=2052 "CompanyName" "QSL Studio"
VIAddVersionKey /LANG=2052 "LegalCopyright" "QSL Studio contributors"

!define MUI_ABORTWARNING
!define MUI_ABORTWARNING_TEXT "是否退出安装向导？"
!define MUI_ICON "${ART}\QSLStudio.ico"
!define MUI_UNICON "${ART}\QSLStudio.ico"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "${ART}\header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "${ART}\header.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${ART}\welcome.bmp"
!define MUI_WELCOMEPAGE_TITLE "安装 QSL 卡片工坊"
!define MUI_WELCOMEPAGE_TEXT "此向导将安装 QSL 卡片工坊 ${APP_VERSION}，包括 18 套卡片模板和离线用户手册。$\r$\n$\r$\n升级前请关闭正在运行的 QSL 卡片工坊。"
!insertmacro MUI_PAGE_WELCOME
!define MUI_COMPONENTSPAGE_TEXT_TOP "干净重装请选择「从空白状态开始」，清除上次安装保留的应用数据。"
!insertmacro MUI_PAGE_COMPONENTS
!define MUI_DIRECTORYPAGE_TEXT_TOP "选择安装目录。覆盖升级时请保留原目录，也可以选择其他盘的可写文件夹。"
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE CheckInstallDir
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "安装完成"
!define MUI_FINISHPAGE_TEXT "可从开始菜单打开 QSL 卡片工坊。首次使用时，可在制作引导中打开用户手册。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\QSLStudio.exe"
!define MUI_FINISHPAGE_RUN_TEXT "打开 QSL 卡片工坊"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!define MUI_COMPONENTSPAGE_TEXT_TOP "卸载默认清除应用设置、台站资料和标志缓存。需要保留时，可取消勾选。"
!insertmacro MUI_UNPAGE_COMPONENTS
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Function .onInit
    SetShellVarContext current
    SetRegView 32
    ${IfNot} ${AtLeastWinXP}
        MessageBox MB_OK|MB_ICONSTOP "此程序需要 Windows XP SP3 或更新版本的 Windows。" /SD IDOK
        SetErrorLevel 2
        Abort
    ${EndIf}
    ${If} ${IsWinXP}
    ${AndIfNot} ${AtLeastServicePack} 3
        MessageBox MB_OK|MB_ICONSTOP "请先为 Windows XP 安装 Service Pack 3，再运行安装程序。" /SD IDOK
        SetErrorLevel 2
        Abort
    ${EndIf}
    ReadRegDWORD $0 HKLM "${NET4_KEY}" "Install"
    ${If} $0 != 1
    ${AndIf} ${RunningX64}
        SetRegView 64
        ReadRegDWORD $0 HKLM "${NET4_KEY}" "Install"
        SetRegView 32
    ${EndIf}
    ${If} $0 != 1
        MessageBox MB_YESNO|MB_ICONEXCLAMATION "安装需要 .NET Framework 4.0 或更高的 4.x 版本。安装运行库后，请重新运行此安装程序。$\r$\n$\r$\n是否打开 Microsoft 下载页面？" /SD IDNO IDNO MissingRuntime
        ExecShell "open" "https://www.microsoft.com/en-us/download/details.aspx?id=17718"
        MissingRuntime:
        SetErrorLevel 2
        Abort
    ${EndIf}
    Call SelectReset
    ReadRegStr $PreviousInstallDir HKCU "${APP_REG}" "InstallDir"
    ${If} $PreviousInstallDir != ""
        GetFullPathName $PreviousInstallDir "$PreviousInstallDir"
        ${If} ${FileExists} "$PreviousInstallDir\QSLStudio.exe"
            ClearErrors
            GetTempFileName $1 "$PreviousInstallDir"
            ${If} ${Errors}
            ${OrIf} $1 == ""
                StrCpy $MigrateFrom "$PreviousInstallDir"
            ${Else}
                Delete "$1"
                ; Some legacy installers put a protected executable in a writable folder.
                System::Call 'kernel32::CreateFileW(w "$PreviousInstallDir\QSLStudio.exe", i 0x40000000, i 7, p 0, i 3, i 128, p 0) p.r2 ?e'
                Pop $3
                ${If} $2 != -1
                    System::Call 'kernel32::CloseHandle(p r2)'
                ${ElseIf} $3 == 5
                    StrCpy $MigrateFrom "$PreviousInstallDir"
                ${EndIf}
            ${EndIf}
            ${If} $MigrateFrom != ""
                GetFullPathName $0 "$INSTDIR"
                ${If} $0 == $PreviousInstallDir
                    StrCpy $INSTDIR "${USER_INSTDIR}"
                ${EndIf}
            ${EndIf}
        ${EndIf}
    ${EndIf}
    ClearErrors
FunctionEnd

Function CheckInstallDir
    StrCmp $INSTDIR "" DirectoryInUse
    GetFullPathName $3 "$INSTDIR"
    StrCmp $3 "" +2
    StrCpy $INSTDIR $3
    StrCmp $INSTDIR $EXEDIR DirectoryInUse
    StrCmp $INSTDIR $MigrateFrom ProtectedDirectory
    StrCmp $PreviousInstallDir $INSTDIR DirOK
    ; A previously used installation can be selected even after moving to another disk.
    IfFileExists "$INSTDIR\QSLStudio.exe" 0 CheckContents
    IfFileExists "$INSTDIR\QSLStudio.exe.config" 0 CheckContents
    IfFileExists "$INSTDIR\Uninstall.exe" DirOK
    CheckContents:
    ClearErrors
    FindFirst $0 $1 "$INSTDIR\*.*"
    IfErrors DirOK
    DirectoryScan:
    StrCmp $1 "" EmptyDirectory
    StrCmp $1 "." ScanNext
    StrCmp $1 ".." ScanNext
    FindClose $0
    Goto DirectoryInUse
    ScanNext:
    ClearErrors
    FindNext $0 $1
    IfErrors EmptyDirectory
    Goto DirectoryScan
    EmptyDirectory:
    FindClose $0
    DirOK:
    ClearErrors
    Return
    ProtectedDirectory:
    MessageBox MB_OK|MB_ICONEXCLAMATION "原安装目录无法写入。请选择有写入权限的文件夹。" /SD IDOK
    SetErrorLevel 3
    Abort
    DirectoryInUse:
    MessageBox MB_OK|MB_ICONEXCLAMATION "请选择空文件夹或 QSL 卡片工坊的现有安装目录。" /SD IDOK
    SetErrorLevel 3
    Abort
FunctionEnd

!macro ReuseLogoFile name
    ${If} ${FileExists} "$PreviousInstallDir\Logos\${name}"
    ${AndIfNot} ${FileExists} "$INSTDIR\Logos\${name}"
        CreateDirectory "$INSTDIR\Logos"
        CopyFiles /SILENT "$PreviousInstallDir\Logos\${name}" "$INSTDIR\Logos"
    ${EndIf}
!macroend

!macro CreateQslShortcut PATH
    CreateShortCut "${PATH}" "$INSTDIR\QSLStudio.exe" "" "$INSTDIR\${SHORTCUT_ICON}" 0 SW_SHOWNORMAL "" "制作双面通联确认卡"
!macroend

!macro RefreshQslItem PATH
    ; SHCNE_UPDATEITEM, SHCNF_PATHW | SHCNF_FLUSHNOWAIT
    System::Call 'shell32::SHChangeNotify(i 0x00002000, i 0x2005, w "${PATH}", p 0)'
!macroend

Function WriteDesktopShortcut
    ClearErrors
    CreateDirectory "${QSL_DESKTOP}"
    !insertmacro CreateQslShortcut "${QSL_DESKTOP}\${APP_NAME}.lnk"
    IfErrors DesktopWriteDone
    !insertmacro RefreshQslItem "${QSL_DESKTOP}\${APP_NAME}.lnk"
    DesktopWriteDone:
FunctionEnd

Section "QSL 卡片工坊（必选）" Main
    SectionIn RO
    Call CheckInstallDir
    CreateDirectory "$INSTDIR"
    ClearErrors
    GetTempFileName $0 "$INSTDIR"
    ${If} ${Errors}
    ${OrIf} $0 == ""
        MessageBox MB_OK|MB_ICONEXCLAMATION "无法写入所选目录。请重新选择安装目录。" /SD IDOK
        SetErrorLevel 4
        Abort
    ${EndIf}
    Delete "$0"
    StrCpy $ReplacingInstall "0"
    ${If} ${FileExists} "$INSTDIR\QSLStudio.exe"
    ${AndIf} ${FileExists} "$INSTDIR\Uninstall.exe"
        StrCpy $ReplacingInstall "1"
    ${EndIf}
    ClearErrors
    SetOutPath "$INSTDIR"
    File "${PAYLOAD}\QSLStudio.exe"
    File "/oname=${SHORTCUT_ICON}" "${ART}\QSLStudio.ico"
    File "${PAYLOAD}\QSLStudio.exe.config"
    File "${PAYLOAD}\README.txt"
    File "${PAYLOAD}\ASSETS.txt"
    File "${PAYLOAD}\LICENSE.txt"
    SetOutPath "$INSTDIR\Templates"
    File "${PAYLOAD}\Templates\ScenicPostcard.qslcard"
    File "${PAYLOAD}\Templates\Logbook.qslcard"
    File "${PAYLOAD}\Templates\SpecialEvent.qslcard"
    !ifndef SKIP_LOGO_PREP
        File "${PAYLOAD}\Templates\FullBleed.qslcard"
        File "${PAYLOAD}\Templates\Bureau.qslcard"
        File "${PAYLOAD}\Templates\Postal.qslcard"
        File "${PAYLOAD}\Templates\Label.qslcard"
        File "${PAYLOAD}\Templates\Satellite.qslcard"
        File "${PAYLOAD}\Templates\SWL.qslcard"
        File "${PAYLOAD}\Templates\Eyeball.qslcard"
        File "${PAYLOAD}\Templates\Callsign.qslcard"
        File "${PAYLOAD}\Templates\Handwritten.qslcard"
        File "${PAYLOAD}\Templates\Contest.qslcard"
        File "${PAYLOAD}\Templates\DomesticPostal.qslcard"
        File "${PAYLOAD}\Templates\OpenNote.qslcard"
        File "${PAYLOAD}\Templates\PortraitPhoto.qslcard"
        File "${PAYLOAD}\Templates\PortraitLog.qslcard"
        File "${PAYLOAD}\Templates\PortraitPostal.qslcard"
    !endif
    IfErrors InstallFailed
    SetOutPath "$INSTDIR"
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    IfErrors InstallFailed
    CreateDirectory "${QSL_PROGRAMS}\${APP_NAME}"
    !insertmacro CreateQslShortcut "${QSL_PROGRAMS}\${APP_NAME}\${APP_NAME}.lnk"
    IfErrors InstallFailed
    !insertmacro RefreshQslItem "${QSL_PROGRAMS}\${APP_NAME}\${APP_NAME}.lnk"
    ${If} ${FileExists} "${QSL_DESKTOP}\${APP_NAME}.lnk"
        Call WriteDesktopShortcut
        IfErrors InstallFailed
    ${EndIf}
    ReadRegStr $4 HKCU "${UNINSTALL_KEY}" "DisplayVersion"
    ClearErrors
    WriteRegStr HKCU "${APP_REG}" "InstallDir" "$INSTDIR"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "QSL Studio"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\QSLStudio.exe,0"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" 6900
    IfErrors InstallFailed
    ${If} $PreviousInstallDir == $INSTDIR
    ${AndIf} $4 != ""
    ${AndIf} $4 != "${APP_VERSION}"
        StrCpy $1 "$INSTDIR\QSLStudio-$4.ico"
        GetFullPathName $2 "$1"
        ${GetParent} "$2" $3
        ${If} $2 == $1
        ${AndIf} $3 == $INSTDIR
            Delete "$2"
        ${EndIf}
    ${EndIf}
    !ifndef SKIP_LOGO_PREP
        ${If} $PreviousInstallDir != ""
        ${AndIf} $PreviousInstallDir != $INSTDIR
            DetailPrint "正在复制已下载的标志。"
            !insertmacro ReuseLogoFile "CRAC.png"
            !insertmacro ReuseLogoFile "CRAC-Round.jpg"
            !insertmacro ReuseLogoFile "CRSA-Archive.gif"
            !insertmacro ReuseLogoFile "ARRL-Gold.jpg"
            !insertmacro ReuseLogoFile "ARRL-Print.jpg"
            !insertmacro ReuseLogoFile "JARL.png"
            !insertmacro ReuseLogoFile "JARL-Centennial.png"
            !insertmacro ReuseLogoFile "IOTA.png"
            !insertmacro ReuseLogoFile "SOTA.jpg"
        ${EndIf}
    !endif
    Goto InstallDone
    InstallFailed:
    MessageBox MB_OK|MB_ICONSTOP "安装未完成。请关闭 QSL 卡片工坊，检查安装目录的写入权限和可用空间后重试。" /SD IDOK
    SetErrorLevel 4
    Abort
    InstallDone:
    ${If} $ReplacingInstall == "1"
        Delete "$INSTDIR\Demo-QSL.pdf"
    ${EndIf}
    !insertmacro RefreshQslItem "$INSTDIR\QSLStudio.exe"
    !ifndef QA_PER_USER
        ClearErrors
        ExecWait '"$INSTDIR\QSLStudio.exe" --clean-qa-shortcuts QSLStudio' $0
        ${If} ${Errors}
        ${OrIf} $0 != 0
            DetailPrint "旧测试快捷方式清理未完成。"
        ${EndIf}
        ClearErrors
    !endif
    SetErrorLevel 0
SectionEnd

Section /o "创建桌面快捷方式" DesktopShortcut
    SetOutPath "$INSTDIR"
    IfFileExists "${QSL_DESKTOP}\${APP_NAME}.lnk" DesktopExists
    Call WriteDesktopShortcut
    DesktopExists:
SectionEnd

!ifndef SKIP_LOGO_PREP
!ifdef QA_PER_USER
Section /o "下载常用协会和活动标志" OfficialLogos
!else
Section "下载常用协会和活动标志" OfficialLogos
!endif
    SetOutPath "$INSTDIR"
    DetailPrint "正在下载标志原图…"
    ExecWait '"$INSTDIR\QSLStudio.exe" --prepare-logos' $0
    ${If} $0 != 0
        DetailPrint "部分标志未能下载。安装后可在「协会标志」中重试。"
    ${EndIf}
SectionEnd
!endif

!macro ClearQslUserData
    DetailPrint "正在清除应用数据。"
    ClearErrors
    ExecWait '"$INSTDIR\QSLStudio.exe" --clear-user-data ${REG_NAME}' $0
    ${If} ${Errors}
        StrCpy $0 1
    ${EndIf}
    ${If} $0 == 3
        MessageBox MB_OK|MB_ICONEXCLAMATION "请关闭正在运行的 QSL 卡片工坊，再清除应用数据。" /SD IDOK
    ${ElseIf} $0 != 0
        MessageBox MB_OK|MB_ICONEXCLAMATION "应用数据清理未完成。请检查应用数据目录的访问权限后重试。" /SD IDOK
    ${EndIf}
    ${If} $0 != 0
        SetErrorLevel 5
        Abort
    ${EndIf}
!macroend

Section /o "从空白状态开始" ResetInstallData
    !insertmacro ClearQslUserData
SectionEnd

Function SelectReset
    ${GetParameters} $0
    ClearErrors
    ${GetOptions} $0 "/RESETDATA" $1
    ${IfNot} ${Errors}
        !insertmacro SelectSection ${ResetInstallData}
    ${EndIf}
    ClearErrors
FunctionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${Main} "安装编辑器、卡片模板和离线用户手册，并添加开始菜单快捷方式。"
    !insertmacro MUI_DESCRIPTION_TEXT ${DesktopShortcut} "在当前 Windows 账户的桌面添加快捷方式。"
    !insertmacro MUI_DESCRIPTION_TEXT ${ResetInstallData} "清除原有设置、台站资料和标志缓存。下次启动时从空白状态开始。"
    !ifndef SKIP_LOGO_PREP
        !insertmacro MUI_DESCRIPTION_TEXT ${OfficialLogos} "联网下载 CRAC、CRSA、ARRL、JARL、IOTA 和 SOTA 标志，下载后可离线使用。"
    !endif
!insertmacro MUI_FUNCTION_DESCRIPTION_END

!macro DeleteQslShortcut PATH
    ${If} ${FileExists} "${PATH}"
        SetFileAttributes "${PATH}" NORMAL
        Delete "${PATH}"
        System::Call 'shell32::SHChangeNotify(i 4, i 5, w "${PATH}", p 0)'
    ${EndIf}
!macroend

Function un.onInit
    SetShellVarContext current
    SetRegView 32
    ReadRegStr $0 HKCU "${APP_REG}" "InstallDir"
    StrCmp $0 "" UninstallPathError
    GetFullPathName $0 "$0"
    GetFullPathName $1 "$INSTDIR"
    StrCmp $0 $1 UninstallPathOK
    UninstallPathError:
    MessageBox MB_OK|MB_ICONSTOP "无法确认安装目录。请从 Windows 的已安装程序列表启动卸载。" /SD IDOK
    SetErrorLevel 3
    Abort
    UninstallPathOK:
    Call un.SelectKeepData
FunctionEnd

Section "un.清除全部应用数据" ResetUninstallData
    !insertmacro ClearQslUserData
SectionEnd

Section "un.QSL 卡片工坊（必选）" RemoveApp
    SectionIn RO
    Delete "$INSTDIR\QSLStudio.exe"
    IfFileExists "$INSTDIR\QSLStudio.exe" AppStillRunning
    Delete "$INSTDIR\QSLStudio.exe.config"
    Delete "$INSTDIR\${SHORTCUT_ICON}"
    Delete "$INSTDIR\README.txt"
    Delete "$INSTDIR\ASSETS.txt"
    Delete "$INSTDIR\LICENSE.txt"
    Delete "$INSTDIR\Demo-QSL.pdf"
    Delete "$INSTDIR\Templates\ScenicPostcard.qslcard"
    Delete "$INSTDIR\Templates\Logbook.qslcard"
    Delete "$INSTDIR\Templates\SpecialEvent.qslcard"
    !ifndef SKIP_LOGO_PREP
        Delete "$INSTDIR\Templates\FullBleed.qslcard"
        Delete "$INSTDIR\Templates\Bureau.qslcard"
        Delete "$INSTDIR\Templates\Postal.qslcard"
        Delete "$INSTDIR\Templates\Label.qslcard"
        Delete "$INSTDIR\Templates\Satellite.qslcard"
        Delete "$INSTDIR\Templates\SWL.qslcard"
        Delete "$INSTDIR\Templates\Eyeball.qslcard"
        Delete "$INSTDIR\Templates\Callsign.qslcard"
        Delete "$INSTDIR\Templates\Handwritten.qslcard"
        Delete "$INSTDIR\Templates\Contest.qslcard"
        Delete "$INSTDIR\Templates\DomesticPostal.qslcard"
        Delete "$INSTDIR\Templates\OpenNote.qslcard"
        Delete "$INSTDIR\Templates\PortraitPhoto.qslcard"
        Delete "$INSTDIR\Templates\PortraitLog.qslcard"
        Delete "$INSTDIR\Templates\PortraitPostal.qslcard"
    !endif
    RMDir "$INSTDIR\Templates"
    !ifndef SKIP_LOGO_PREP
        Delete "$INSTDIR\Logos\CRAC.png"
        Delete "$INSTDIR\Logos\CRAC-Round.jpg"
        Delete "$INSTDIR\Logos\CRSA-Archive.gif"
        Delete "$INSTDIR\Logos\ARRL-Gold.jpg"
        Delete "$INSTDIR\Logos\ARRL-Print.jpg"
        Delete "$INSTDIR\Logos\JARL.png"
        Delete "$INSTDIR\Logos\JARL-Centennial.png"
        Delete "$INSTDIR\Logos\IOTA.png"
        Delete "$INSTDIR\Logos\SOTA.jpg"
        RMDir "$INSTDIR\Logos"
    !endif
    !insertmacro DeleteQslShortcut "${QSL_PROGRAMS}\${APP_NAME}\${APP_NAME}.lnk"
    RMDir "${QSL_PROGRAMS}\${APP_NAME}"
    !insertmacro DeleteQslShortcut "${QSL_DESKTOP}\${APP_NAME}.lnk"
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
    DeleteRegKey HKCU "${APP_REG}"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"
    Goto UninstallDone
    AppStillRunning:
    MessageBox MB_OK|MB_ICONEXCLAMATION "请先关闭 QSL 卡片工坊，再运行卸载程序。" /SD IDOK
    SetErrorLevel 4
    Abort
    UninstallDone:
SectionEnd

Function un.SelectKeepData
    ${GetParameters} $0
    ClearErrors
    ${GetOptions} $0 "/KEEPDATA" $1
    ${IfNot} ${Errors}
        !insertmacro UnselectSection ${ResetUninstallData}
    ${EndIf}
    ClearErrors
FunctionEnd

!insertmacro MUI_UNFUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${RemoveApp} "移除程序文件和快捷方式。"
    !insertmacro MUI_DESCRIPTION_TEXT ${ResetUninstallData} "清除应用设置、台站资料和标志缓存。重新安装后从空白状态开始。"
!insertmacro MUI_UNFUNCTION_DESCRIPTION_END
