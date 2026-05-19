# TailAdmin Next.js - 企业级管理后台模板

TailAdmin 是一个基于 Next.js 15 和 Tailwind CSS 构建的现代化企业级管理后台模板，提供完整的仪表板解决方案和丰富的业务组件。

![TailAdmin - Next.js 仪表板预览](./banner.png)

## 1. 项目概述

TailAdmin 是一个功能完整的企业级管理后台模板，集成了多种业务场景的仪表板设计。项目采用现代化的技术栈，提供响应式设计、暗色模式支持、可折叠侧边栏等企业级特性。

### 核心功能
- **多场景仪表板**：电商、分析、营销、CRM、股票、SaaS、物流等7种业务场景
- **AI 功能模块**：文本生成、代码生成、图像生成、视频生成等AI工具
- **完整业务组件**：用户管理、订单处理、产品管理、发票系统、任务管理
- **数据可视化**：基于 ApexCharts 的图表组件，支持线图、柱状图、饼图
- **响应式设计**：完全适配移动端和桌面端，支持暗色/亮色主题切换
- **现代化交互**：拖拽功能、实时聊天、日历管理、文件管理器

## 2. 技术栈

### 前端框架
- **Next.js 15**：基于 React 的全栈框架，支持 App Router 和 React Server Components
- **React 19**：最新的 React 版本，提供更好的性能和开发体验
- **TypeScript**：提供完整的类型安全和更好的开发体验

### 样式和UI
- **Tailwind CSS 4.0**：实用优先的 CSS 框架，提供响应式设计和暗色模式支持
- **自定义组件库**：基于 Tailwind CSS 构建的完整 UI 组件系统

### 数据可视化
- **ApexCharts**：强大的图表库，支持多种图表类型
- **React ApexCharts**：React 版本的 ApexCharts 集成

### 功能增强
- **FullCalendar**：完整的日历组件，支持事件管理
- **React DnD**：拖拽功能支持，用于任务管理和文件操作
- **React Dropzone**：文件上传和拖拽功能
- **Swiper**：现代化的轮播和滑动组件

### 开发工具
- **ESLint**：代码质量检查
- **Prettier**：代码格式化
- **PostCSS**：CSS 后处理器
- **SVGR**：SVG 图标处理

### 快速链接
- [访问网站](https://tailadmin.com)
- [文档](https://tailadmin.com/docs)
- [下载](https://tailadmin.com/download)
- [Figma 设计文件](https://www.figma.com/community/file/1463141366275764364)
- [获取专业版](https://tailadmin.com/pricing)

### 演示
- [免费版本](https://nextjs-free-demo.tailadmin.com)
- [专业版本](https://nextjs-demo.tailadmin.com)

## 3. 项目架构

### 整体架构设计
项目采用 Next.js 15 的 App Router 架构，基于 React Server Components 和客户端组件的混合模式。整体架构分为以下几个层次：

#### 路由层 (App Router)
- **管理后台路由组** `(admin)`：包含所有管理功能页面
- **全宽页面路由组** `(full-width-pages)`：包含认证、错误页面等
- **嵌套路由结构**：支持多级路由和布局嵌套

#### 组件层 (Components)
- **业务组件**：按功能模块组织的业务逻辑组件
- **UI 组件**：可复用的基础 UI 组件
- **布局组件**：页面布局和导航组件

#### 状态管理层 (Context)
- **主题管理**：全局主题状态和切换逻辑
- **侧边栏管理**：侧边栏展开/收起状态管理

#### 工具层 (Utils)
- **样式工具**：Tailwind CSS 类名合并和条件样式处理
- **自定义 Hooks**：可复用的业务逻辑封装

### 数据流向
1. **服务端渲染**：页面初始数据通过 Next.js SSR 获取
2. **客户端状态**：用户交互状态通过 React Context 管理
3. **组件通信**：通过 Props 和 Context 进行组件间数据传递
4. **样式管理**：通过 Tailwind CSS 和自定义工具函数处理样式

### 模块关系
- **布局模块**：提供统一的页面布局和导航结构
- **业务模块**：独立的业务功能模块，可单独开发和维护
- **共享模块**：通用的组件和工具，供各业务模块使用

## 安装

### 前置要求
要开始使用 TailAdmin，请确保您已安装并设置以下前置要求：

- Node.js 18.x 或更高版本（建议使用 Node.js 20.x 或更高版本）

### 克隆仓库
使用以下命令克隆仓库：

```bash
git clone https://github.com/TailAdmin/free-nextjs-admin-dashboard.git
```

> Windows 用户：如果在克隆时遇到问题，请将仓库放置在驱动器根目录附近。

1. 安装依赖：
    ```bash
    npm install
    # 或
    yarn install
    ```
    > 如果在安装过程中遇到对等依赖错误，请使用 `--legacy-peer-deps` 标志。

2. 启动开发服务器：
    ```bash
    npm run dev
    # 或
    yarn dev
    ```

## 4. 目录结构

```
nextjs-admin/
├── src/
│   ├── app/                           # Next.js 15 App Router 页面
│   │   ├── (admin)/                   # 管理后台页面组
│   │   │   ├── (home)/                # 仪表板页面组
│   │   │   │   ├── analytics/         # 分析仪表板
│   │   │   │   ├── crm/              # CRM 仪表板
│   │   │   │   ├── logistics/        # 物流仪表板
│   │   │   │   ├── marketing/        # 营销仪表板
│   │   │   │   ├── saas/             # SaaS 仪表板
│   │   │   │   └── stocks/           # 股票仪表板
│   │   │   ├── (others-pages)/       # 其他功能页面
│   │   │   │   ├── (ai)/             # AI 功能模块
│   │   │   │   │   ├── code-generator/    # 代码生成器
│   │   │   │   │   ├── image-generator/   # 图像生成器
│   │   │   │   │   ├── text-generator/    # 文本生成器
│   │   │   │   │   └── video-generator/   # 视频生成器
│   │   │   │   ├── (chart)/          # 图表页面
│   │   │   │   ├── (ecommerce)/      # 电商功能页面
│   │   │   │   ├── (email)/          # 邮件功能页面
│   │   │   │   ├── (forms)/          # 表单页面
│   │   │   │   ├── (support)/        # 支持页面
│   │   │   │   ├── (tables)/         # 表格页面
│   │   │   │   └── (task)/           # 任务管理页面
│   │   │   ├── (ui-elements)/        # UI 元素展示页面
│   │   │   ├── layout.tsx            # 管理后台布局
│   │   │   └── page.tsx              # 默认仪表板首页
│   │   ├── (full-width-pages)/       # 全宽页面组
│   │   │   ├── (auth)/               # 认证页面组
│   │   │   │   ├── signin/           # 登录页面
│   │   │   │   ├── signup/           # 注册页面
│   │   │   │   ├── reset-password/   # 重置密码
│   │   │   │   └── two-step-verification/ # 两步验证
│   │   │   ├── (error-pages)/        # 错误页面组
│   │   │   │   ├── error-404/        # 404 错误页面
│   │   │   │   ├── error-500/        # 500 错误页面
│   │   │   │   ├── error-503/        # 503 错误页面
│   │   │   │   └── maintenance/      # 维护页面
│   │   │   ├── coming-soon/          # 即将上线页面
│   │   │   └── success/              # 成功页面
│   │   ├── layout.tsx                # 根布局组件
│   │   ├── globals.css               # 全局样式文件
│   │   ├── not-found.tsx             # 404 页面
│   │   └── favicon.ico               # 网站图标
│   ├── components/                   # React 组件库
│   │   ├── ai/                       # AI 功能组件
│   │   ├── analytics/                # 分析组件
│   │   ├── auth/                     # 认证相关组件
│   │   ├── calendar/                 # 日历组件
│   │   ├── cards/                    # 卡片组件
│   │   ├── charts/                   # 图表组件
│   │   ├── chats/                    # 聊天组件
│   │   ├── common/                   # 通用组件
│   │   ├── crm/                      # CRM 业务组件
│   │   ├── ecommerce/                # 电商业务组件
│   │   ├── email/                    # 邮件组件
│   │   ├── form/                     # 表单组件
│   │   ├── header/                   # 头部组件
│   │   ├── layout/                   # 布局组件
│   │   ├── tables/                   # 表格组件
│   │   ├── task/                     # 任务管理组件
│   │   ├── ui/                       # 基础 UI 组件
│   │   └── user-profile/             # 用户资料组件
│   ├── context/                      # React Context 状态管理
│   │   ├── SidebarContext.tsx        # 侧边栏状态管理
│   │   └── ThemeContext.tsx          # 主题状态管理
│   ├── hooks/                        # 自定义 React Hooks
│   │   ├── useGoBack.ts              # 返回功能 Hook
│   │   └── useModal.ts               # 模态框管理 Hook
│   ├── icons/                        # SVG 图标库
│   │   └── index.tsx                 # 图标导出文件
│   ├── layout/                       # 布局相关组件
│   │   ├── AppHeader.tsx             # 应用头部
│   │   ├── AppSidebar.tsx            # 应用侧边栏
│   │   ├── Backdrop.tsx              # 背景遮罩
│   │   └── SidebarWidget.tsx         # 侧边栏小部件
│   ├── utils/                        # 工具函数
│   │   └── index.ts                  # 样式工具函数
│   └── svg.d.ts                      # SVG 类型声明
├── public/                           # 静态资源目录
│   └── images/                       # 图片资源
├── package.json                      # 项目依赖配置
├── next.config.ts                    # Next.js 配置文件
├── tsconfig.json                     # TypeScript 配置
├── eslint.config.mjs                 # ESLint 配置
├── prettier.config.js                # Prettier 配置
└── postcss.config.mjs                # PostCSS 配置
```

## 5. 核心文件说明

### 项目入口文件和配置文件

#### `src/app/layout.tsx`
- **作用**：根布局组件，提供全局的 HTML 结构和 Context 提供者
- **功能**：集成主题提供者、侧边栏提供者，设置全局字体和样式
- **关键特性**：支持暗色模式、响应式设计

#### `src/app/(admin)/layout.tsx`
- **作用**：管理后台布局组件，提供统一的页面布局结构
- **功能**：管理侧边栏状态、头部导航、主内容区域布局
- **关键特性**：响应式侧边栏、动态内容边距、路由特定样式

#### `next.config.ts`
- **作用**：Next.js 配置文件
- **功能**：配置 SVG 处理、Webpack 设置
- **关键特性**：支持 SVG 作为 React 组件导入

#### `tsconfig.json`
- **作用**：TypeScript 配置文件
- **功能**：设置编译选项、路径映射、类型检查规则
- **关键特性**：支持路径别名 `@/*` 映射到 `./src/*`

### 核心业务逻辑实现

#### `src/context/ThemeContext.tsx`
- **作用**：全局主题状态管理
- **功能**：提供主题切换、本地存储持久化、DOM 类名管理
- **关键特性**：支持亮色/暗色主题切换，自动保存用户偏好

#### `src/context/SidebarContext.tsx`
- **作用**：侧边栏状态管理
- **功能**：管理侧边栏展开/收起、悬停状态、移动端显示
- **关键特性**：响应式侧边栏行为，支持多种交互模式

#### `src/layout/AppSidebar.tsx`
- **作用**：应用侧边栏组件
- **功能**：提供导航菜单、用户信息、小部件展示
- **关键特性**：多级菜单结构、图标集成、权限控制

#### `src/layout/AppHeader.tsx`
- **作用**：应用头部组件
- **功能**：提供搜索、通知、用户菜单、主题切换
- **关键特性**：响应式设计、用户交互、状态同步

### 数据模型和API接口

#### `src/components/ecommerce/EcommerceMetrics.tsx`
- **作用**：电商指标展示组件
- **功能**：显示客户数量、订单统计、收入数据等关键指标
- **关键特性**：数据可视化、趋势分析、响应式布局

#### `src/components/charts/`
- **作用**：图表组件库
- **功能**：提供线图、柱状图、饼图等多种图表类型
- **关键特性**：基于 ApexCharts、支持交互、主题适配

#### `src/components/tables/`
- **作用**：表格组件库
- **功能**：提供基础表格、数据表格、分页等功能
- **关键特性**：排序、筛选、分页、响应式设计

### 关键组件和服务模块

#### `src/utils/index.ts`
- **作用**：工具函数库
- **功能**：提供样式类名合并、条件样式处理
- **关键特性**：基于 clsx 和 tailwind-merge，支持条件样式

#### `src/hooks/useModal.ts`
- **作用**：模态框管理 Hook
- **功能**：提供模态框状态管理、打开/关闭逻辑
- **关键特性**：可复用、类型安全、状态管理

#### `src/hooks/useGoBack.ts`
- **作用**：返回功能 Hook
- **功能**：提供页面返回逻辑、历史记录管理
- **关键特性**：浏览器历史集成、用户友好

#### `src/icons/index.tsx`
- **作用**：图标库导出文件
- **功能**：统一管理所有 SVG 图标组件
- **关键特性**：类型安全、按需导入、可扩展

## 开发指南

### 可用脚本

```bash
# 启动开发服务器
npm run dev

# 构建生产版本
npm run build

# 启动生产服务器
npm start

# 运行代码检查
npm run lint
```

### 主要依赖

- **Next.js 15.4.3**：React 框架
- **React 19**：用户界面库
- **TypeScript 5**：类型安全
- **Tailwind CSS 4.0.0**：CSS 框架
- **ApexCharts 4.3.0**：图表库
- **FullCalendar 6.1.15**：日历组件
- **React DnD 16.0.1**：拖拽功能

## 版本信息

### 当前版本：2.2.0

#### 主要特性
- **Next.js 15.4.3**：使用最新的 App Router 和 React Server Components
- **React 19**：最新的 React 特性支持
- **Tailwind CSS 4.0**：现代化的 CSS 框架
- **TypeScript 5**：完整的类型安全
- **多场景仪表板**：7种不同业务场景的仪表板设计
- **AI 功能模块**：集成文本、代码、图像、视频生成功能
- **响应式设计**：完全适配移动端和桌面端
- **暗色模式**：内置主题切换功能

#### 技术亮点
- 基于 Next.js 15 App Router 的现代化架构
- React Server Components 和客户端组件的混合使用
- 完整的 TypeScript 类型支持
- 基于 ApexCharts 的数据可视化
- 可折叠侧边栏和响应式布局
- 完整的认证和权限管理系统

## 许可证

TailAdmin Next.js 免费版本在 MIT 许可证下发布。

## 支持与贡献

### 获取支持
- 如果您发现这个项目有帮助，请考虑在 GitHub 上给它一个星标
- 您的支持帮助我们继续开发和维护这个模板
- 欢迎提交 Issue 和 Pull Request

### 贡献指南
- 欢迎贡献代码、文档和设计
- 请查看项目的贡献指南了解如何参与开发
- 提交代码前请确保通过所有测试和代码检查

### 联系方式
- 官方网站：https://tailadmin.com
- 项目文档：https://tailadmin.com/docs
- GitHub 仓库：https://github.com/TailAdmin/free-nextjs-admin-dashboard
- 设计文件：https://www.figma.com/community/file/1463141366275764364
