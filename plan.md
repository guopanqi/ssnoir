SSNoir 原型执行计划                                                            
                                                                                  
  ## 技术选型                                                                     
                                                                                  
   层         │ 技术                            │ 职责
  ────────────┼─────────────────────────────────┼─────────────────────────────────
   表现层     │ Raylib-cs                       │ 卡片渲染、点击交互
   宿主层     │ C# (.NET 8)                     │ GameState、解释器桥接、节点转换
   脚本层     │ Schemy (stefanschneider/schemy) │ 场景定义、规则系统、节点树
  ──────                                                                          
  ## 工程结构                                                                     
                                                                                  
    ssnoir/                                                                       
    ├── ssnoir.csproj                                                             
    ├── src/                                                                      
    │   ├── Core/                                                                 
    │   │   ├── GameNode.cs            # C# 节点：Name, Children, Effect          
    │   │   ├── GameState.cs           # 全局状态 Dictionary<string, object>      
    │   │   └── SceneManager.cs        # LoadScene / Refresh / ExecuteEffect      
    │   ├── Scripting/                                                            
    │   │   ├── SchemeInterpreter.cs   # Schemy 封装：Eval / Apply / LoadFile /   
  RegisterNative                                                                  
    │   │   ├── NodeConverter.cs       # SchemeVal → List<GameNode>，effect 存为  
  Action 闭包                                                                     
    │   │   └── NativeFunctions.cs     # 注册 get-global / set-global!            
    │   └── Rendering/                                                            
    │       ├── RaylibRenderer.cs      # 主渲染循环、布局、输入分发               
    │       └── CardWidget.cs          # 单张卡片绘制（hover/pressed 状态）       
    ├── scenes/                                                                   
    │   ├── home.scm                                                              
    │   └── office.scm                 # 预留                                     
    └── scripts/                                                                  
        └── stdlib.scm                 # node / define-rule / on-action 的纯      
  Scheme 实现                                                                     
  ──────                                                                          
  ## 架构图                                                                       
                                                                                  
    ┌────────────────────────────────────────────────┐                            
    │              表现层（Raylib）                  │                            
    │                                                │                            
    │  [ 家 > 客厅 ]  ← 面包屑 + 返回按钮           │                             
    │                                                │                            
    │  ┌────────┐  ┌────────┐  ┌────────┐           │                             
    │  │ 敲门   │  │ 窗户   │  │ 进门   │  ...       │                            
    │  │[行动]  │  │[场所]  │  │[行动]  │           │                             
    │  └────────┘  └────────┘  └────────┘           │                             
    │                                                │                            
    │  底部：money: 50  health: 100  loc: 家         │                            
    └────────────────────────────────────────────────┘                            
             │ List<GameNode>        ↑ node.Effect()                              
             ▼                      │                                             
    ┌────────────────────────────────────────────────┐                            
    │                C# 宿主层                       │                            
    │  GameState   SchemeInterpreter   NodeConverter │                            
    │  Dict<>      Eval/Apply/Load     SchemeVal→C#  │                            
    └────────────────────────────────────────────────┘                            
             │ get-global            ↑ set-global!                                
             ▼                      │                                             
    ┌────────────────────────────────────────────────┐                            
    │           Scheme 脚本层（home.scm）             │                           
    │  局部状态           节点定义       入口函数     │                           
    │  (define unlock?)   (define-node)  (get-world) │                            
    │  (define knock 0)   world-contents (on-action) │                            
    └────────────────────────────────────────────────┘                            
  ──────                                                                          
  ## UI 交互设计                                                                  
                                                                                  
  卡片网格：节点以固定宽度卡片横向排列，自动换行。每张卡片显示节点名（大字）+     
  类型标注「行动 / 场所」（小字）+ hover 高亮。                                   
                                                                                  
  导航逻辑：                                                                      
                                                                                  
    点击有 children 的卡片                                                        
      → 卡片列表换成 children                                                     
      → 面包屑追加层级                                                            
      → 出现「← 返回」按钮                                                        
                                                                                  
    点击有 effect 的卡片                                                          
      → 执行 effect（调用 Scheme lambda）                                         
      → 调用 (on-action)（规则检查）                                              
      → 调用 (get-world)（重新求值）                                              
      → 刷新卡片列表                                                              
                                                                                  
    点击「← 返回」                                                                
      → 导航栈弹出，回到父节点                                                    
      → 不触发 on-action                                                          
                                                                                  
  底部调试面板：常驻显示 GlobalState 中的 money / health /                        
  location，验证脚本效果用。                                                      
  ──────                                                                          
  ## 分阶段执行                                                                   
                                                                                  
  ### Phase 1 — 项目骨架                                                          
                                                                                  
  1. 创建  ssnoir.csproj （.NET 8），添加 NuGet： Raylib-cs 、 Schemy             
  2. 实现  GameState （Dictionary + 泛型 Get/Set）                                
  3. 实现  SchemeInterpreter  封装 Schemy                                         
  4. 实现  NativeFunctions ：注册  get-global  /  set-global!                     
  5. 写  stdlib.scm ： node （返回关联列表）、 define-rule 、 on-action           
  6. 验证：Console 加载脚本，打印  (get-world)  返回结构                          
                                                                                  
  ### Phase 2 — 脚本层                                                            
                                                                                  
  7. 写  home.scm ：敲门计数 → unlock → 进门；垃圾桶；money 条件节点              
  8. 实现  NodeConverter ：递归翻译 SchemeVal →  GameNode ，effect 封装为  Action 
  9. 实现  SceneManager ： LoadScene  /  Refresh  /  ExecuteEffect                
  10. 验证：Console 执行 effect，再打印节点，确认状态变化正确                     
                                                                                  
  ### Phase 3 — 渲染层                                                            
                                                                                  
  11. 实现  CardWidget ：圆角矩形 + 标题 + 类型标注 + hover 动画                  
  12. 实现  RaylibRenderer                                                        
  ：卡片网格布局、面包屑导航、返回按钮、底部状态面板、鼠标点击分发                
  13. 连通主循环： BeginDrawing → Renderer.Draw → EndDrawing                      
                                                                                  
  ### Phase 4 — DSL 验证                                                          
                                                                                  
  14. 敲门 ×3 →  on-action  触发 → 进门卡片出现                                   
  15. 踢垃圾桶 → 垃圾卡片出现 → 清理 → 消失                                       
  16. money 不足 → 条件节点不显示 → 充钱后刷新出现                                
  17. 进门 →  LoadScene("office")  → 局部状态丢弃，全局状态保留                   
  ──────                                                                          
  ## 最小里程碑                                                                   
                                                                                  
    启动 → home.scm → 显示 [ 家 ] [ 垃圾桶 ]                                      
    点击「家」→ 进入，显示 [ 敲门 ] [ 窗户 ]，出现返回按钮                        
    点击「敲门」×3 → 规则触发 → 新增「进门」卡片                                  
    点击「进门」→ 切换 office.scm → 底部 location 更新                            
    点击返回 → 回到父节点（不触发 on-action）                                     
  ──────                                                                          
  ## 关键设计决策                                                                 
                                                                                  
   决策       │ 选择                                       │ 原因
  ────────────┼────────────────────────────────────────────┼──────────────────────
   状态存活   │ Scheme 解释器持续运行                      │ set! 语义干净
   全局状态   │ C# Dictionary，Scheme 通过原生函数读写     │ 跨场景持久，存档简单
   场景切换   │ 每场景一个 .scm，切换时重新 LoadFile       │ 局部状态自然丢弃
   节点动态性 │ define 成 lambda，get-world 时才调用       │ 每次都读最新状态
   规则系统   │ define-rule 声明式注册，on-action 统一遍历 │ 规则相互独立
   Unity 迁移 │ 只改 RaylibRenderer → UGUI，其余不动       │ 解释器和 .scm 完全复 
  ──────                                                                          
  