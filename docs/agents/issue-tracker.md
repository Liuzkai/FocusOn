# Issue tracker: Local Markdown

## 文件约定

- 每个功能一个目录：`.scratch/<feature-slug>/`；FocusOn 第一版使用 `focuson`。
- 规格保存为 `spec.md`。
- 开发票保存为 `issues/<NN>-<slug>.md`，从 `01` 编号，每票一个文件，按依赖顺序排列。
- 规格和票据顶部记录 `Status:`；分诊角色见 `triage-labels.md`。
- 开发票用 `Blocked by:` 列出前置票号；无依赖写 `None`。
- 讨论追加到文件底部的 `## Comments`。

## 技能操作

- 发布：在上述位置创建或更新相应文件。
- 获取：读取指定规格或票据的全文及 Comments。
- 开始实施：仅选择仍开放且前置票全部已关闭的开发票。

## 完成约定（项目补充）

开发票满足验收条件，完成必需检查、代码审查和提交后，在 Comments 追加完成说明、验证结果与提交引用，再设置 `Status: closed`。Blocked by 所列开发票全部为 closed 时，依赖解除；ready-for-agent 只表示可开始实施。

全部开发票关闭且整体规格通过验收后，关闭规格文件。closed 是本地完成状态，不是分诊标签。
