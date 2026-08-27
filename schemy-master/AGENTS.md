# Schemy Fork — 说明与改动记录

本目录是 [Microsoft/schemy](https://github.com/microsoft/schemy) 的 **fork**,供 SSNoir 使用。

- **原版项目说明**(它是什么、设计理念、嵌入用法)见上游仓库:<https://github.com/microsoft/schemy>。
  本目录不再保留上游 README,只保留下面三块:工作约定、能力总览、改动记录。
- 目的:为 SSNoir(及未来项目)提供一个独立、Unity 友好的 C# Scheme 库;
  **库本身不含 SSNoir 概念**,SSNoir 只是使用者。
- 写 SSNoir 脚本的人看 [../skills/write-scheme/SKILL.md](../skills/write-scheme/SKILL.md);本文件是底层解释器的事实来源。

---

## 工作约定

1. **改了本目录任何源文件,必须运行 `./build-unity-plugin.sh`** 重建 `Engine/Plugins/schemy.dll`,
   否则 Unity 用的还是旧版本。
2. **每一处改动(修复 / 新增)都在文末「改动日志」追加一条**:带日期,写清
   「改了哪个文件 / 为什么 / 行为前后差异」。

---

## 能力总览（当前 fork 支持什么）

**语法 / 特殊形式**
- `define`、`lambda`(固定参 / `(lambda args)` 全捕获 / `(a b . rest)` dotted rest 均支持)
- 字符串支持 `\"`、`\\`、`\n`、`\r`、`\t` 转义;单引号和 Unicode 弯引号是普通字符
- `if`、`cond`、`begin`、`quote` / `quasiquote`、`set!`、`define-macro`
- `let` / `let*` / `letrec` / 命名 `let`(named let);空 `(let)` 也建立作用域
- `and` / `or`(**短路**)、`when`、`unless`
- **不支持**:`case`、`call/cc`、方括号 `[...]`

**内置函数(`Builtins.cs`)**
- 算术 `+ - * /`、`modulo`、`remainder`、`quotient`、`abs`、`min`、`max`
- 比较 `= < <= > >=`;相等 `eq?`、`eqv?`、`equal?`
- 类型谓词 `null?`、`pair?`、`list?`、`number?`、`string?`、`symbol?`、`boolean?`、`procedure?`
- 列表 `cons car cdr list length list-ref reverse append`(变参)`map`(多列表)`apply`
- `not`、`range`、`error`、`assert`、`display` / `write` / `newline`、`load`
- `cXr` 组合(`cadr caddr cadddr cddr caar …`,在 `Init.cs` 的宏前奏里)
- 真值:**只有 `#f` 为假**(`0`、`""`、`'()` 都为真)

**可选 `stdlib.scm`**(本目录根,需手动 `load`,SSNoir 未自动加载):
`filter`、`for-each`、`assoc`、`member`、`fold-left/right`、`iota`、`gcd`、`lcm` 等。

> 输出函数 `display`/`write`/`newline` 写到 `interpreter.Output`(默认 `Console.Out`);
> Unity 下可设 `interpreter.Output` 为自定义 `TextWriter`。

---

## 改动日志（按时间倒序）

### 2026-08-23 — 变参算术的零参数与单参数语义

**文件**:`src/schemy/Builtins.cs`、`src/test/Program.cs`。
**问题**:`+ - * /` 原本共用二元函数的 `Aggregate`包装；单参数时根本不调用运算，
导致 `(- 3)` 错误返回 `3`、`(/ 4)` 错误返回 `4`，零参数则由 LINQ 抛出无语义的空序列异常。
**行为变化**:`(+)` 返回 `0`，`(*)` 返回 `1`，`(- x)` 返回 `-x`，`(/ x)` 返回浮点倒数；
`(-)` 与 `(/)` 无参数时明确报 arity error，两参数及以上仍按原有数值体系从左到右计算。

### 2026-08-04 — 宏前奏改为编译进程序集（IL2CPP / WebGL 修复）

**文件**:新增 `src/schemy/Init.cs`;`src/schemy/Schemy.cs`(`GetInitializeReaders`);
`src/schemy/schemy.csproj`(去掉 `<EmbeddedResource>`);删除 `src/schemy/init.ss`。
**问题**:前奏原本是嵌入资源,靠 `Assembly.GetManifestResourceStream("init.ss")` 读回。
CLR 直接跑程序集时没问题,**Unity IL2CPP 下取不到**——托管程序集被转成 C++,资源没跟过去。
`GetInitializeReaders` 当时找不到就返回空列表,**不报错**,解释器于是在没有
`let`/`cond`/`and`/`or` 的状态下静默启动。宿主脚本里第一个 `(let () …)` 不再被宏展开,
成了字面空表,报 `Syntax error: ()`——错误现场离真正的原因隔了十万八千里。
SSNoir 的表现是 WebGL 版整个世界加载失败(无节点、无人物),Editor 一切正常。
**行为变化**:前奏改为 `Init.cs` 里的 `const string`,是代码而非资源,构建怎么处理程序集都带得走;
`init.ss` 删除,`Init.Source` 是唯一来源(仍按 Scheme 编辑,verbatim 字符串,只需注意双引号)。
DLL 之外的可选宿主级 `.init.ss` 机制不变。

### 2026-07-25 — unspecified 值的错误信息

**文件**：`src/schemy/Utils.cs`(`ConvertType` + `PrintExpr`)。
**性质**：**不是语义修复,只是让哑错自解释**。`(if test conseq)` 缺 alternate 时 expand 期补
`None.Instance`(`Schemy.cs`),`cond` 全部落空同理——这是 R5RS/R7RS 规定的 unspecified,
上游行为正确,**不应改成返回 `'()`**(那会掩盖真错误)。
**问题**:把 unspecified 喂给需要真值的内置函数(最常见是 `append`)时,只报
`Cannot convert Schemy.None to type List`1`,完全不提示 None 从哪来,排查成本极高。
**行为变化**:`ConvertType` 对 `None` 单独抛出说明性错误,指出 unspecified 的两个来源
(`if` 缺 alternate / `cond` 落空)与修法(补 `'()`),并注明 `when`/`unless` 是安全的
(本 fork 的 `init.ss` 让它们返回 `'()`);`PrintExpr(None)` 由 `Schemy.None` 改为 `#<unspecified>`。
类型错误的原有信息不变。

### 2026-07-02 — 字符串转义解析

**文件**:`src/schemy/Schemy.cs`、`src/test/Program.cs`。
**原因**:reader 能识别反斜杠转义 token,但 `ParseAtom` 仅去掉首尾引号,导致 `\"`、`\\` 等没有被解码。
**行为变化**:字符串现正确解码 `\"`、`\\`、`\n`、`\r`、`\t`;未知转义直接抛出 `SyntaxError`。

### 2026-06-21 — 空 `(let)` / `(let*)` 作用域（回归修复）

**文件**:`src/schemy/init.ss`(`let` 两处 + `let*`)。
**性质**:**我们 fork 自己引入的回归**——上游空 `let` 本就展开成 `((lambda () …))`(正确), sonnet 在 fork 重写 `init.ss` 时改成了 `(begin …)`,导致不建立作用域、内部 `define` 泄漏到全局。
**后果**:SSNoir 每个地点 `(define 地点 (let () (define helper…) …))` 的 helper 全泄漏到全局, 同名 helper 跨地点串台(`饭店` 渲染出 `废弃仓库` 的节点,触发渲染树重名断言)。
**修复**:空绑定改回 `((lambda () …))`,与非空 `let` 一致建立新帧。

### fork 初版 — 相对上游的主要改动

- **目标框架**:`netstandard2.0`(原 `net4.5.2`),兼容 Unity 与现代 .NET。
- **API**:`ICallable` 改 `public`;`Procedure.Parameters` / `Environment.FromVariablesAndValues`
  改用 `LambdaParams`;新增构造参数 `output`;新增 `Interpreter.Output`、`Evaluate(string)`。
- **语法修复**:dotted rest 变参;真值语义(只有 `#f` 假);`not`/`append`/`apply`/`map` 标准化。
- **`init.ss` 新增宏**:`letrec`、`let*`、named `let`、`and`、`or`、`when`、`unless` + `cXr`。
- **新增内置**:`number? procedure? pair? eqv? modulo remainder quotient abs min max error display write newline`。
- **Unity 安全**:`Assembly.Location` 包 `try/catch`(IL2CPP 下不再崩,且不需要反射 hack)。
