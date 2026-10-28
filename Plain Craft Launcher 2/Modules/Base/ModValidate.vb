' 输入校验规则已迁移到 PCL.Core；此处保留 VB 侧的兼容入口。
Public Module ModValidate

    ''' <summary>
    ''' 进行输入验证，并返回错误原因。如果没有错误则返回空字符串。
    ''' </summary>
    Public Function Validate(Text As String, ValidateRules As IEnumerable(Of Validate)) As String
        Return Validation.Validate(Text, ValidateRules)
    End Function

End Module
