# sonic | rapid expression evaluation for .NET
_sonic_ is a rapid evaluation engine for mathematical expressions. It can parse and evaluate strings containing mathematical expressions.

_sonic_ is also the expression evaluator we use in our commercial products. It is a core component of our real-time simulation tools for virtual vehicle and ADAS prototyping and is continuously stress tested in a demanding environment. Its development and maintenance is funded by our product sales.

The guiding principles for _sonic_ are (in that order):

1. **Performance**: _sonic_ is aiming to be the fastest expression evaluator for .NET. It is optimized for both, multi pass evaluation of the same expression and single pass evaluation of many different expressions.
2. **Usability**: _sonic_ is designed to be easy to use. It comes with a sane default configuration, an understandable documentation and a simple API. The most common use-cases should be fast out-of-the-box.
3. **Maintainability**: _sonic_ is designed to be easy to maintain. It is written in a clean and readable code style and comes with a comprehensive test and benchmarking suite. The NuGet package introduces no transient dependencies and is fully self-contained.

_sonic_ originally started as a fork of [_Jace.NET_ by Pieter De Rycke](https://github.com/pieterderycke/Jace), which is no longer actively maintained. It is not a drop-in replacement for _Jace.NET_, but you should be able to switch to _sonic_ with minimal effort.

_sonic_ is **considerably faster** than _Jace.NET_ (see benchmarks below). It contains numerous **bugfixes** and a lot of **maintenance work** over the latest _Jace.NET_ release (1.0.0). Many of them were originally suggested and developed by the community for _Jace.NET_, but never merged due to the dormant state of the project. See the [changelog](CHANGELOG.md) for details and a complete list.

## Build Status
| branch | status |
| -----  | -----   |
| main (development) | [![Build status](https://github.com/adletec/jace/actions/workflows/dotnet.yml/badge.svg?branch=master)](https://github.com/adletec/jace/actions/workflows/dotnet.yml?query=branch%3Amain) |
| release |  ![Build status](https://github.com/adletec/jace/actions/workflows/release.yml/badge.svg)|

## Quick Start
_sonic_ can parse and evaluate strings containing mathematical expressions. These expressions may rely on variables, which can be defined at runtime.

Consider this simple example:

```csharp
var variables = new Dictionary<string, double>();
variables.Add("var1", 2.5);
variables.Add("var2", 3.4);

var engine = Evaluator.CreateWithDefaults();
double result = engine.Evaluate("var1*var2", variables); // 8.5
```

The Evaluator comes with out-of-the-box support for many arithmetic (`+`, `-`, `*`, `/`, `...`), trigonometric (`sin`, `cos`, `atan`, `...`) statistic (`avg`, `max`, `min`, `median`, `...`), and simple boolean logic (`if`, `ifless`, `ifequal`, ...) functions.

You can add your own domain-specific functions. This example adds a conversion function from length in feet (`ft`) to meter (`m`):

```csharp
var engine = Evaluator.Create()
    .AddFunction("ft2m", (Func<double, double>)((a) => a * 0.3048))
    .Build();
double result = engine.Evaluate("ft2m(30)"); // 9.144
```

You can find more examples below.

_sonic_ can execute formulas in two modes: **dynamic compilation mode** and **interpreted mode**. If **dynamic compilation mode** is used, _sonic_ will create a dynamic method at runtime and will generate the MSIL opcodes necessary for the native execution of the evaluation. If a formula is re-evaluated with other variables, _sonic_ will take the dynamically generated method from its cache (if enabled, which it is by default). Dynamic compilation mode is a lot faster when evaluating an expression, but has a higher overhead when building the evaluator.

As a rule of thumb, you should use **dynamic compilation mode** if you are evaluating the same expressions multiple times with different variables, and **interpreted mode** if you are evaluating many different expressions only once.

Additionally, for specific use-cases (e.g. Unity with IL2CPP) dynamic code generation can be limited. In those cases, you can use the **interpreted mode** as a fallback.

## Installation
_sonic_ is available via [nuget](https://www.nuget.org/packages/Adletec.Sonic):

```bash
dotnet add package Adletec.Sonic --version 1.2.0
```

## Usage
### Evaluating an Expression
#### Directly Evaluate an Expression
The easiest way to evaluate an expression is to use the `Evaluate()`-method of the `Evaluator`:

```csharp
Dictionary<string, double> variables = new Dictionary<string, double>();
variables.Add("var1", 2.5);
variables.Add("var2", 3.4);

var engine = Evaluator.CreateWithDefaults();
double result = engine.Evaluate("var1*var2", variables);
```

#### Create a Delegate for an Expression
_sonic_ can also create a [delegate (Func)](https://learn.microsoft.com/en-us/dotnet/api/system.func-2?view=net-7.0) from your expression which will take the variable dictionary as argument:

```csharp
var engine = Evaluator.CreateWithDefaults();
Func<Dictionary<string, double>, double> evaluate = engine.CreateDelegate("var1+2/(3*otherVariable)");

Dictionary<string, double> variables = new Dictionary<string, double>();
variables.Add("var1", 2);
variables.Add("otherVariable", 4.2);
	
double result = evaluate(variables);
```

If you intend to evaluate the same expression repeatedly with different variables, you should use this method. It will avoid the overhead of retrieving the delegate from the cache, based on the expression string. On the other hand, there is no performance benefit in using this method if you are only evaluating the expression once.

### Using Mathematical Functions
You can also use mathematical functions in your expressions:

```csharp
Dictionary<string, double> variables = new Dictionary<string, double>();
variables.Add("var1", 2.5);
variables.Add("var2", 3.4);

var engine = Evaluator.CreateWithDefaults();
double result = engine.Evaluate("logn(var1,var2)+4", variables);
```

#### Built-in Functions

_sonic_ supports most common functions out-of-the-box:

| Function |  Signature | Parameters|
|-----|-----|-----|
| Sine |  `sin(a)` | `a`: angle in radians |
| Cosine |  `cos(a)` |`a`: angle in radians |
| Secant |  `sec(a)` |`a`: angle in radians |
| Cosecant |  `csc(a)` |`a`: angle in radians |
| Tangent |  `tan(a)` |`a`: angle in radians |
| Cotangent |  `cot(a)` |`a`: angle in radians |
| Arcsine |  `asin(a)` |`a`: angle in radians |
| Arccosine |  `acos(a)` |`a`: angle in radians |
| Arctangent |  `atan(a)` |`a`: angle in radians |
| Arccotangent |  `acot(a)` |`a`: angle in radians |
| Natural logarithm |  `loge(a)` | `a`: number whose logarithm is to be found |
| Common logarithm | `log10(a)` |`a`: number whose logarithm is to be found |
| Logarithm | `logn(a, b)` | `a`: number whose logarithm is to be found<br/>`b`: base of the logarithm |
| Square root | `sqrt(a)` | `a`: number whose square root is to be found |
| Absolute | `abs(a)` | `a`: number whose absolute value is to be found |
| If | `if(a,b,c)` | `a`: boolean expression, e.g. `x > 2`<br/>`b`: result if true(`!= 0`)<br/>`c`: result if false (`== 0`)|
| If less | `ifless(a,b,c,d)` | `a`: first value<br/>`b`: second value<br/>`c`: result if `a < b`<br/>`d`: result if `a >= b`|
| If more | `ifmore(a,b,c,d)` | `a`: first value<br/>`b`: second value<br/>`c`: result if `a > b`<br/>`d`: result if `a <= b`|
| If equal | `ifequal(a,b,c,d)` | `a`: first value<br/>`b`: second value<br/>`c`: result if `a == b`<br/>`d`: result if `a != b`|
| Ceiling | `ceiling(a)` | `a`: number to be rounded towards `+∞`|
| Floor | `floor(a)` | `a`: number to be rounded towards `-∞`|
| Truncate | `truncate(a)` | `a`: number to be truncated (to integral part)|
| Round | `round(a)` | `a`: number to be rounded (to even)|
| Maximum | `max(a,b,...)` | `a,b,...`: series of numbers to find the maximum of |
| Minimum | `min(a,b,...)` | `a,b,...`: series of numbers to find the minimum of |
| Average | `avg(a,b,...)` | `a,b,...`: series of numbers to find the average of |
| Median | `median(a,b,...)` | `a,b,...`: series of numbers to find the median of |
| Sum | `median(a,b,...)` | `a,b,...`: series of numbers to build the sum of |
| Random | `random()` | no parameters, returns random number in `[0..1]` |

The function names are reserved keywords and cannot be overwritten. If you need to override a function, you can globaly disable the built-in functions using the configuration (see below).

#### Custom Functions
You can define your own functions using the `.AddFunction()`-method while instanciating the evaluator.

```csharp
var engine = Evaluator.Create()
    .AddFunction("ft2m", (Func<double, double>)((a) => a * 0.3048))
    .Build();
double result = engine.Evaluate("ft2m(30)"); // 9.144
```

The `.AddFunction()`-method provides overloads for functions with up to 16 parameters. If you want to process an arbitrary amount of parameters, you can use dynamic functions:

```csharp
double MyCustomSumFormula(params double[] a)
{
    return a.Sum();
}

var engine = Evaluator.Create()
    .AddFunction("customSum", MyCustomSumFormula)
    .Build();
double result = engine.Evaluate("customSum(1,2,3,4,5,6)"); // 21.0
```

Custom function names are overwritable, so you can re-register the same name with a different implementation.

### Using Constants
_sonic_ provides support for pre-compile constants. These constants are taken into account during the optimization phase of the compilation process. I.e., if your expression contains an operation like `2 * pi`, this operation is already evaluated when you build a delegate and the result is cached.

#### Built-in Constants
| Constant | Name |
| ---- | ---- |
| π | `pi` |
| ⅇ | `e` |

The constant names are reserved keywords and cannot be overwritten. If you define a variable with the same name as a constant, the constant will take precedence.

If you need to override a constant, you can globaly disable the built-in constants using the configuration (see below).

#### Custom Constants
You can define your own constants using the `.AddConstant()`-method while instanciating the evaluator.

```csharp
var engine = Evaluator.Create()
    .AddConstant("g", 9.80665)
    .Build();
double result = engine.Evaluate("g*2"); // 19.6133
```

Custom constants will also be taken into account during the optimization phase of the compilation process.

#### Guarded Mode
There are some cases in which _sonic_ can get ambiguous inputs. If there is no sane way to continue, _sonic_ will throw an Exception:

* A constant name collides with a function name
* A function name collides with a variable name

Both cases are easy to find during AST creation, since a text token can't be unambiguously resolved.

Other ambiguous inputs are a little more subtle which makes them harder to find:

* The given variables include a variable name which is also a constant name
* A constant is defined multiple times with different values
* A function is defined multiple times with different implementations

_sonic_ doesn't automatically recognize those cases, since it's explicitly designed to have a sane default which doesn't require additional checks:

* Constants always have precedence over variables
* Of two different constant definitions, the latter wins
* Of two different function definitions, the latter wins

However, all of those cases might be potential sources of error, so for some use-cases, it might not be the desired behavior to silently ignore the ambiguous input.

In that case, you can enable _Guarded Mode_ by calling `.EnableGuardedMode()` when building your `Evaluator` instance:

```csharp
var evaluator = Evaluator.Create()
    .EnableGuardedMode()
    .Build();
```

_sonic_ will then actively check if it encounters any of the above cases and throw an `ArgumentException` if it does.
These checks are pretty expensive, so _Guarded Mode_ is disabled by default. It is recommended to only use _Guarded Mode_ if performance is not of importance
or at least less important than the additional checks.

Still, _Guarded Mode_ can help you to find problems in your code which might otherwise be hard to track down. It can be beneficial to use it while developing an
application (e.g. in a _Debug_ configuration) and disable it in your production code (_Release_ configuration).

## Configuration
The `Evaluator`-builder also allows you to configure the evaluator. The following options are available:

| Option | Values (bold = default)| Comment |
| ---- | ---- | ----|
| `UseCulture(CultureInfo cultureInfo)` | **`CultureInfo.CurrentCulture`**<br/>[`CultureInfo.*`](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo?view=net-7.0) | Determines the number format (e.g. decimal separator, thousands separator, etc.); defaults to your system default; | 
| `UseExecutionMode(ExecutionMode executionMode)` | **`ExecutionMode.Compiled`**<br/>`ExecutionMode.Interpreted` | `Compiled` will dynamically compile the evaluation to MSIL which grants the best evaluation performance, but comes with the overhead of compilation. If you are using a platform where dynamic compilation is restricted, or don't want to re-evaluate the same expressions, you should use `Interpreted`.|
| `EnableCache()` / `DisableCache()` | **`enabled`** | Can be used to disable the formula cache if set to `false`. The formula cache keeps a copy of the optimized AST for every given formula string, so it can be re-used. This makes subsequent evaluations of the same formula significantly faster. If you don't intend to re-evaluate the same expressions, you can disable the cache. This will reduce memory consumption and improve initial evaluation performance. |
| `EnableOptimizer()` / `DisableOptimizer()` | **`enabled`** | Can be used to disable the optimizer if set to `false`. The optimizer will pre-evaluate parts of the equation which do not depend on variables, including multiplications with 0 or 0 exponents. You can disable the optimizer if you know for a fact that the given expressions won't contain foldable constants or if you don't intend to re-evalute the same expressions.|
| `EnableCaseSensitivity()` / `DisableCaseSensitivity()` | **`enabled`** | Determines wether the provided variable names will be evaluated case-sensitive (enabled) or case-insensitive (disabled). If you don't absolutely need case-insensitivity, you should keep this option set to case-sensitive since this has a notable performance impact. |
| `EnableDefaultFunctions()` / `DisableDefaultFunctions()` | **`enabled`** | Can be used to disable the built-in functions. |
| `EnableDefaultConstants()` / `DisableDefaultConstants()` | **`enabled`** | Can be used to disable the built-in constants. |
| `EnableGuardedMode()` / `DisableGuardedMode()` | **`disabled`** | Enables guarded mode. This means that the engine will throw exceptions for non-fatal errors, i.e. if it receives ambiguous input for which a sane default exists, but which is possibly not what the user intended. You can use this if you want to pin down hard to find bugs in your expressions. Since it comes with a severe performance impact, it is recommended to keep guarded mode disabled in production. Alas, if you prioritize validation over performance, you might decide otherwise. |
| `UseCacheMaximumSize(int cacheMaximumSize)` | **`500`**<br/> | The number of expressions to keep in the cache. |
| `UseCacheReductionSize(int cacheReductionSize)` | **`50`** | The number of expressions to drop from the cache once it reaches its maximum size ([FIFO](https://en.wikipedia.org/wiki/FIFO_(computing_and_electronics))). |

All options will be applied to the evaluator and all delegates created from it. The configuration is immutable. I.e., if you want to change the configuration of an evaluator, you have to create a new one.

## Performance
### Benchmark
_sonic_ is primed to deliver great performance out-of-the-box. It comes with a comprehensive benchmarking suite which is easy to run and understand.

You can use the benchmark to compare the performance of different configurations when evaluating specific expressions in a specific way or environment.
The benchmark is based on [BenchmarkDotNet](https://benchmarkdotnet.org/).

To run the benchmark, you can use the following command:

```bash
dotnet run -c Release dotnet run -c Release --project Adletec.Sonic.Benchmark/Adletec.Sonic.Benchmark.csproj
```

Take a look at Program.cs to see how to extend or adjust the benchmark.

#### Comparison with other Libraries
To get a better understanding of the performance of _sonic_, the benchmark also includes a set of comparisons with other popular expression evaluators for .NET.

**Disclaimer**: Keep in mind that all those libraries have unique features and performance is only one aspect when choosing the right library for your project. The following comparison is in no way a statement towards the general superiority/inferiority of any of the listed libraries.

##### Benchmark Setup
We're using a simple benchmark which will take the same three equations and evaluate them using the same values with all libraries:

* Expression A - Simple expression `var1 + var2 * var3 / 2`
* Expression B - Balanced expression including functions and constants: `sin(var1) + cos(var2) + pi^2`
* Expression C - Foldable expression: `(var1 + var2 * var3 / 2) * 0 + 0 / (var1 + var2 * var3 / 2) + (var1 + var2 * var3 / 2)^0`

To make sure the expressions will be re-evaluated, we're incrementing each variable on every iteration.

The benchmark runs all iterations on the same machine (MacBook Pro 2021, M1 Max).

##### Benchmark Results

###### Default Settings
The following table shows the time in seconds it takes the benchmark to complete 100.000 evaluations of each expression using the default settings of each library. As a reference, it also contains the time it takes to evaluate the same expressions using hardcoded C#.

| Library | Expression A | Expression B | Expression C |
|---- | ---- |  ---- | ---- | 
| Hardcoded C# | 1.036 ms | 1.029 ms | 1.036 ms |
| _sonic_ | 6.376 ms | 9.145 ms | 2.853 ms |
| Jace.NET |  22.637 ms  | 24.918 ms | 29.710 ms |
| NCalc | 33.579 ms | 51.400 ms| 127.165 ms |

Keep in mind that this is a very specific benchmark and not entirely fair. The frameworks are using different default settings which might not be optimal for the given benchmark. You can get a better understanding of the performance of each library by running the benchmark yourself and adjusting the expressions to your needs.

## Project Origin
When we originally forked _Jace.NET_, the idea was to give it a new home. At the time of writing, the upstream repository still has 11 unmerged pull requests (which are mostly merged into _sonic_ now). It takes time and effort to maintain an open source library, and we're very thankful for the fantastic foundation which is _Jace.NET_. Pieter De Rycke's original code is still a very large part of _sonic_, and so is the work of all who have contributed to _Jace.NET_ over the years.

However, as a company, using a software component which seems to be in a dormant state introduces a significant risk. Our decision to create a maintained fork of _Jace.NET_ was mainly driven by the necessity to mitigate this risk, while still using the best expression evaluator available for .NET.

While _Jace.NET_ is still the origin of this project, _sonic_ has diverged from its ancestor in quite some way, including major changes to the API. These changes were - in part - necessary to fix some architectural problems we encountered in the code base. Being able to add functions and constants after parsing and folding steps are completed introduced a lot of headaches and complexity in the usage of _Jace.NET_ while adding almost no real world benefit over doing the same steps before any evaluation has taken place.

It also showed that the _Jace.NET_ API evolved to its current state rather than following a clear design, which makes it harder to understand: which of the three ways to evaluate an expression is the right one for a specific use case? How do they differ at all?

After putting many hours of work into our _Jace.NET_ fork, we decided that it no longer made sense to maintain API compatibility, and this was the birth hour of _sonic_.

When compared to _Jace.NET_, _sonic_ shows to be at least as fast as _Jace.NET_. Often, _sonic_ outperforms _Jace.NET_ by a significant factor, the notable exception being re-evaluation with disabled cache. This is due to a still existing bug in _Jace.NET_ which prevents the user from _actually_ disabling the cache.

We're using _sonic_ in our commercial products and it's stress tested many thousand hours per year. We fixed a lot of bugs, we're constantly benchmarking and testing new kinds of expressions to make sure that we don't overfit to a specific kind of test.

The success of our products would not have been possible without the great work of Pieter De Rycke and the _Jace.NET_ community. Open sourcing _sonic_ and offering a new home to the _Jace.NET_ community is our way of giving back and to say _Thank You_..

## Contributing
Your contributions are welcome. To streamline the process, take note of the following best practices:

* Always discuss your goal and solution in an issue before implementing a solution or even opening a pull request. This potentially saves a lot of time and rework compared to having the same conversation after you open your pull request.
* Do one thing (feature, bugfix, change,...) per pull request. If you need one thing to do another, break them up in seperate requests.
* Please keep pull requests to a manageable size, so we can clearly understand the changes and intent.
* Add tests to prove your code is doing what it says and make sure there are no broken tests.
* Run the benchmark suite and make sure that your change didn't (negatively) impact performance. If so, clearly state why this is necessary.

## FAQ
**Q:** Why is the main project still on .NET Standard 1.6?<br/>
**A:** While upgrading the project to .NET 7.0 would give us access to a couple of nice language features, there is no real benefit for the user of the library. But if we'd upgrade, we'd also force everyone who's currently on .NET < 7.0 to upgrade, too - which might break their use-case. This might change in the future, if there is a significant performance or even maintainability benefit from a newer target framework. At the moment, this is not the case.


**Q:** What is the purpose of demo application in the solution?<br/>
**A:** The original _Jace.NET_ contained a demo application just like this. It shows how an expression is parsed and illustrates the AST derived from it. This is a cross-platform version of the same demo, built using Avalonia UI.  It's a nice little thing if you want to get a better understanding of what _sonic_ does internally. It's not a good example of how to use _sonic_, though. If you want to see a lot of usage examples, take a look at the tests.
