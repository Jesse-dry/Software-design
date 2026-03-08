function To-UnityUnicode($str) {
    $sb = [System.Text.StringBuilder]::new()
    foreach ($c in $str.ToCharArray()) {
        if ([int]$c -gt 127) {
            $null = $sb.Append("\u{0:x4}" -f [int]$c)
        } else {
            $null = $sb.Append($c)
        }
    }
    return $sb.ToString()
}

$b0 = To-UnityUnicode "当你读取到这段数据时，零号法庭的记忆复现程序已经启动。我是 13 号档案员，也就是你现在重演的`u300c罪徒`u300d。接下来的记忆，藏着我看到的全部真相，也是你活下去的唯一依仗……"
$b1 = To-UnityUnicode "【永序记忆】，将客户的记忆完整抽取并加密托管在离线服务器中，客户大脑将彻底清除这段记忆。你是工号 13，永序中心金牌记忆搬运工，身处归档区，安保系统已经全面启动……你的核心目标：活着逃出这栋大楼……"
$b2 = To-UnityUnicode "《绝对静默》：档案员仅作为数据的容器与搬运工。严禁查看、严禁拷贝、严禁对客户记忆产生任何主观解读。数据即是数据，无关善恶。"
$b3 = To-UnityUnicode "他们说数据即是数据，无关善恶。但我看到了恶，就无法无视。绝对静默，从来不是我们闭嘴的理由。当你收回记忆，就有了扳倒他们的武器……"

Write-Host "b0: $b0"
Write-Host "b1: $b1"
Write-Host "b2: $b2"
Write-Host "b3: $b3"

# Read the scene file
$scenePath = "D:\Consensus Protocol\new-one\Assets\Scenes\Memory.Unity"
$content = Get-Content $scenePath -Raw

# Old values
$oldBodies = @"
  fragmentBodies:
  - "\u6a21\u7cca\u7684\u8bb0\u5fc6\u6d6e\u4e0a\u5fc3\u5934\u2026\u2026\u4f60\u770b\u5230\u4e86\u4e00\u6247\u65cb\u8f6c\u7684\u95e8\u3002"
  - "\u6709\u4eba\u5728\u4f4e\u8bed\u2014\u2014\u300c\u4e0d\u8981\u76f8\u4fe1\u4ed6\u4eec\u7ed9\u4f60\u770b\u7684\u300d\u3002"
  - "\u4e00\u5f20\u6cdb\u9ec4\u7684\u5408\u540c\uff0c\u7b7e\u540d\u5904\u88ab\u523b\u610f\u6a21\u7cca\u4e86\u3002"
  - "\u6700\u540e\u7684\u8bb0\u5fc6\u662f\u4e00\u9053\u523a\u773c\u7684\u767d\u5149\uff0c\u7136\u540e\u662f\u65e0\u5c3d\u7684\u6c89\u9ed8\u3002"
"@

$newBodies = "  fragmentBodies:`n  - `"$b0`"`n  - `"$b1`"`n  - `"$b2`"`n  - `"$b3`"`n"

Write-Host "=== OLD ==="
Write-Host $oldBodies
Write-Host "=== NEW ==="
Write-Host $newBodies
