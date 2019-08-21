﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jace.Execution;
using Jace.Operations;
using Jace.Tokenizer;
using Jace.Util;

namespace Jace
{
    public delegate TResult DynamicFunc<T, TResult>(params T[] values);

    /// <summary>
    /// The CalculationEngine class is the main class of Jace.NET to convert strings containing
    /// mathematical formulas into .NET Delegates and to calculate the result.
    /// It can be configured to run in a number of modes based on the constructor parameters choosen.
    /// </summary>
    public class CalculationEngine
    {
        private readonly IExecutor executor;
        private readonly Optimizer optimizer;
        private readonly CultureInfo cultureInfo;
        private readonly MemoryCache<string, Func<IDictionary<string, double>, double>> executionFormulaCache;
        private readonly bool cacheEnabled;
        private readonly bool optimizerEnabled;
        private readonly bool adjustVariableCaseEnabled;

        /// <summary>
        /// Creates a new instance of the <see cref="CalculationEngine"/> class with
        /// default parameters.
        /// </summary>
        public CalculationEngine()
            : this(CultureInfo.CurrentCulture, ExecutionMode.Compiled)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="CalculationEngine"/> class. The dynamic compiler
        /// is used for formula execution and the optimizer and cache are enabled.
        /// </summary>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo"/> required for correctly reading floating poin numbers.
        /// </param>
        public CalculationEngine(CultureInfo cultureInfo)
            : this(cultureInfo, ExecutionMode.Compiled)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="CalculationEngine"/> class. The optimizer and 
        /// cache are enabled.
        /// </summary>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo"/> required for correctly reading floating poin numbers.
        /// </param>
        /// <param name="executionMode">The execution mode that must be used for formula execution.</param>
        public CalculationEngine(CultureInfo cultureInfo, ExecutionMode executionMode)
            : this(cultureInfo, executionMode, true, true, true) 
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="CalculationEngine"/> class.
        /// </summary>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo"/> required for correctly reading floating poin numbers.
        /// </param>
        /// <param name="executionMode">The execution mode that must be used for formula execution.</param>
        /// <param name="cacheEnabled">Enable or disable caching of mathematical formulas.</param>
        /// <param name="optimizerEnabled">Enable or disable optimizing of formulas.</param>
        /// <param name="adjustVariableCaseEnabled">Enable or disable auto lowercasing of variables.</param>
        public CalculationEngine(CultureInfo cultureInfo, ExecutionMode executionMode, bool cacheEnabled, bool optimizerEnabled, bool adjustVariableCaseEnabled)
            : this(cultureInfo, executionMode, cacheEnabled, optimizerEnabled, adjustVariableCaseEnabled, true, true, JaceOptions.DefaultCacheMaximumSize, JaceOptions.DefaultCacheReductionSize)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="CalculationEngine"/> class.
        /// </summary>
        /// <param name="options">The <see cref="JaceOptions"/> to configure the behaviour of the engine.</param>
        public CalculationEngine(JaceOptions options)
            : this(options.CultureInfo, options.ExecutionMode, options.CacheEnabled, options.OptimizerEnabled, options.AdjustVariableCase,
                  options.DefaultFunctions, options.DefaultConstants, options.CacheMaximumSize, options.CacheReductionSize)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="CalculationEngine"/> class.
        /// </summary>
        /// <param name="cultureInfo">
        /// The <see cref="CultureInfo"/> required for correctly reading floating poin numbers.
        /// </param>
        /// <param name="executionMode">The execution mode that must be used for formula execution.</param>
        /// <param name="cacheEnabled">Enable or disable caching of mathematical formulas.</param>
        /// <param name="optimizerEnabled">Enable or disable optimizing of formulas.</param>
        /// <param name="defaultFunctions">Enable or disable the default functions.</param>
        /// <param name="defaultConstants">Enable or disable the default constants.</param>
        public CalculationEngine(CultureInfo cultureInfo, ExecutionMode executionMode, bool cacheEnabled, 
            bool optimizerEnabled, bool adjustVariableCaseEnabled, bool defaultFunctions, bool defaultConstants, int cacheMaximumSize, int cacheReductionSize)
        {
            this.executionFormulaCache = new MemoryCache<string, Func<IDictionary<string, double>, double>>(cacheMaximumSize, cacheReductionSize);
            this.FunctionRegistry = new FunctionRegistry(false);
            this.ConstantRegistry = new ConstantRegistry(false);
            this.cultureInfo = cultureInfo;
            this.cacheEnabled = cacheEnabled;
            this.optimizerEnabled = optimizerEnabled;
            this.adjustVariableCaseEnabled = adjustVariableCaseEnabled;

            if (executionMode == ExecutionMode.Interpreted)
                executor = new Interpreter(adjustVariableCaseEnabled);
            else if (executionMode == ExecutionMode.Compiled)
                executor = new DynamicCompiler(adjustVariableCaseEnabled);
            else
                throw new ArgumentException(string.Format("Unsupported execution mode \"{0}\".", executionMode), 
                    "executionMode");

            optimizer = new Optimizer(new Interpreter()); // We run the optimizer with the interpreter 

            // Register the default constants of Jace.NET into the constant registry
            if(defaultConstants)
                RegisterDefaultConstants();

            // Register the default functions of Jace.NET into the function registry
            if(defaultFunctions)
                RegisterDefaultFunctions();
        }

        internal IFunctionRegistry FunctionRegistry { get; private set; }

        internal IConstantRegistry ConstantRegistry { get; private set; }

        public IEnumerable<FunctionInfo> Functions { get { return FunctionRegistry; } }

        public IEnumerable<ConstantInfo> Constants { get { return ConstantRegistry; } }

        public double Calculate(string formulaText)
        {
            return Calculate(formulaText, new Dictionary<string, double>());
        }

        public double Calculate(string formulaText, IDictionary<string, double> variables)
        {
            if (string.IsNullOrEmpty(formulaText))
                throw new ArgumentNullException("formulaText");

            if (variables == null)
                throw new ArgumentNullException("variables");

            if (adjustVariableCaseEnabled)
            {
              variables = EngineUtil.ConvertVariableNamesToLowerCase(variables);
            }
            VerifyVariableNames(variables);

            // Add the reserved variables to the dictionary
            foreach (ConstantInfo constant in ConstantRegistry)
                variables.Add(constant.ConstantName, constant.Value);

            if (IsInFormulaCache(formulaText, null, out var function))
            {
                return function(variables);
            }
            else
            {
                Operation operation = BuildAbstractSyntaxTree(formulaText, new ConstantRegistry(!adjustVariableCaseEnabled));
                function = BuildFormula(formulaText, null, operation);
                return function(variables);
            }
        }

        public FormulaBuilder Formula(string formulaText)
        {
            if (string.IsNullOrEmpty(formulaText))
                throw new ArgumentNullException("formulaText");

            return new FormulaBuilder(formulaText, adjustVariableCaseEnabled, this);
        }

        /// <summary>
        /// Build a .NET func for the provided formula.
        /// </summary>
        /// <param name="formulaText">The formula that must be converted into a .NET func.</param>
        /// <returns>A .NET func for the provided formula.</returns>
        public Func<IDictionary<string, double>, double> Build(string formulaText)
        {
            if (string.IsNullOrEmpty(formulaText))
                throw new ArgumentNullException("formulaText");

            if (IsInFormulaCache(formulaText, null, out var result))
            {
                return result;
            }
            else
            {
                Operation operation = BuildAbstractSyntaxTree(formulaText, new ConstantRegistry(!adjustVariableCaseEnabled));
                return BuildFormula(formulaText, null, operation);
            }
        }

        /// <summary>
        /// Build a .NET func for the provided formula.
        /// </summary>
        /// <param name="formulaText">The formula that must be converted into a .NET func.</param>
        /// <param name="constants">Constant values for variables defined into the formula. They variables will be replaced by the constant value at pre-compilation time.</param>
        /// <returns>A .NET func for the provided formula.</returns>
        public Func<IDictionary<string, double>, double> Build(string formulaText, IDictionary<string, double> constants)
        {
            if (string.IsNullOrEmpty(formulaText))
                throw new ArgumentNullException("formulaText");


            ConstantRegistry compiledConstants = new ConstantRegistry(!adjustVariableCaseEnabled);
            if (constants != null)
            {
                foreach (var constant in constants)
                {
                    compiledConstants.RegisterConstant(constant.Key, constant.Value);
                }
            }

            if (IsInFormulaCache(formulaText, compiledConstants, out var result))
            {
                return result;
            }
            else
            {
                Operation operation = BuildAbstractSyntaxTree(formulaText, compiledConstants);
                return BuildFormula(formulaText, compiledConstants,  operation);
            }
        }

        /// <summary>
        /// Add a function to the calculation engine.
        /// </summary>
        /// <param name="functionName">The name of the function. This name can be used in mathematical formulas.</param>
        /// <param name="function">The implemenation of the function.</param>
        public void AddFunction(string functionName, Func<double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        /// <summary>
        /// Add a function to the calculation engine.
        /// </summary>
        /// <param name="functionName">The name of the function. This name can be used in mathematical formulas.</param>
        /// <param name="function">The implemenation of the function.</param>
        public void AddFunction(string functionName, Func<double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function); 
        }

        /// <summary>
        /// Add a function to the calculation engine.
        /// </summary>
        /// <param name="functionName">The name of the function. This name can be used in mathematical formulas.</param>
        /// <param name="function">The implemenation of the function.</param>
        public void AddFunction(string functionName, Func<double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        /// <summary>
        /// Add a function to the calculation engine.
        /// </summary>
        /// <param name="functionName">The name of the function. This name can be used in mathematical formulas.</param>
        /// <param name="function">The implemenation of the function.</param>
        public void AddFunction(string functionName, Func<double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        /// <summary>
        /// Add a function to the calculation engine.
        /// </summary>
        /// <param name="functionName">The name of the function. This name can be used in mathematical formulas.</param>
        /// <param name="function">The implemenation of the function.</param>
        public void AddFunction(string functionName, Func<double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        /// <summary>
        /// Add a function to the calculation engine.
        /// </summary>
        /// <param name="functionName">The name of the function. This name can be used in mathematical formulas.</param>
        /// <param name="function">The implemenation of the function.</param>
        public void AddFunction(string functionName, Func<double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        /// <summary>
        /// Add a function to the calculation engine.
        /// </summary>
        /// <param name="functionName">The name of the function. This name can be used in mathematical formulas.</param>
        /// <param name="function">The implemenation of the function.</param>
        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, Func<double, double, double, double, double, double, double, double, double, double, double, double, double, double, double, double, double> function)
        {
            FunctionRegistry.RegisterFunction(functionName, function);
        }

        public void AddFunction(string functionName, DynamicFunc<double, double> functionDelegate)
        {
            FunctionRegistry.RegisterFunction(functionName, functionDelegate);
        }

        /// <summary>
        /// Add a constant to the calculation engine.
        /// </summary>
        /// <param name="constantName">The name of the constant. This name can be used in mathematical formulas.</param>
        /// <param name="value">The value of the constant.</param>
        public void AddConstant(string constantName, double value)
        {
            ConstantRegistry.RegisterConstant(constantName, value);
        }

        private void RegisterDefaultFunctions()
        {
            FunctionRegistry.RegisterFunction("sin", (Func<double, double>)((a) => Math.Sin(a)), false);
            FunctionRegistry.RegisterFunction("cos", (Func<double, double>)((a) => Math.Cos(a)), false);
            FunctionRegistry.RegisterFunction("csc", (Func<double, double>)((a) => MathUtil.Csc(a)), false);
            FunctionRegistry.RegisterFunction("sec", (Func<double, double>)((a) => MathUtil.Sec(a)), false);
            FunctionRegistry.RegisterFunction("asin", (Func<double, double>)((a) => Math.Asin(a)), false);
            FunctionRegistry.RegisterFunction("acos", (Func<double, double>)((a) => Math.Acos(a)), false);
            FunctionRegistry.RegisterFunction("tan", (Func<double, double>)((a) => Math.Tan(a)), false);
            FunctionRegistry.RegisterFunction("cot", (Func<double, double>)((a) => MathUtil.Cot(a)), false);
            FunctionRegistry.RegisterFunction("atan", (Func<double, double>)((a) => Math.Atan(a)), false);
            FunctionRegistry.RegisterFunction("acot", (Func<double, double>)((a) => MathUtil.Acot(a)), false);
            FunctionRegistry.RegisterFunction("loge", (Func<double, double>)((a) => Math.Log(a)), false);
            FunctionRegistry.RegisterFunction("log10", (Func<double, double>)((a) => Math.Log10(a)), false);
            FunctionRegistry.RegisterFunction("logn", (Func<double, double, double>)((a, b) => Math.Log(a, b)), false);
            FunctionRegistry.RegisterFunction("sqrt", (Func<double, double>)((a) => Math.Sqrt(a)), false);
            FunctionRegistry.RegisterFunction("abs", (Func<double, double>)((a) => Math.Abs(a)), false);
            FunctionRegistry.RegisterFunction("if", (Func<double, double, double, double>)((a, b, c) => (a != 0.0 ? b : c)), false);
            FunctionRegistry.RegisterFunction("ifless", (Func<double, double, double, double, double>)((a, b, c, d) => (a < b ? c : d)), false);
            FunctionRegistry.RegisterFunction("ifmore", (Func<double, double, double, double, double>)((a, b, c, d) => (a > b ? c : d)), false);
            FunctionRegistry.RegisterFunction("ifequal", (Func<double, double, double, double, double>)((a, b, c, d) => (a == b ? c : d)), false);
            FunctionRegistry.RegisterFunction("ceiling", (Func<double, double>)((a) => Math.Ceiling(a)), false);
            FunctionRegistry.RegisterFunction("floor", (Func<double, double>)((a) => Math.Floor(a)), false);
            FunctionRegistry.RegisterFunction("truncate", (Func<double, double>)((a) => Math.Truncate(a)), false);
            FunctionRegistry.RegisterFunction("round", (Func<double, double>)((a) => Math.Round(a)), false);

            // Dynamic based arguments Functions
            FunctionRegistry.RegisterFunction("max",  (DynamicFunc<double, double>)((a) => a.Max()), false);
            FunctionRegistry.RegisterFunction("min", (DynamicFunc<double, double>)((a) => a.Min()), false);
            FunctionRegistry.RegisterFunction("avg", (DynamicFunc<double, double>)((a) => a.Average()), false);
            FunctionRegistry.RegisterFunction("median", (DynamicFunc<double, double>)((a) => MathExtended.Median(a)), false);
        }

        private void RegisterDefaultConstants()
        {
            ConstantRegistry.RegisterConstant("e", Math.E, false);
            ConstantRegistry.RegisterConstant("pi", Math.PI, false);
        }

        /// <summary>
        /// Build the abstract syntax tree for a given formula. The formula string will
        /// be first tokenized.
        /// </summary>
        /// <param name="formulaText">A string containing the mathematical formula that must be converted 
        /// into an abstract syntax tree.</param>
        /// <returns>The abstract syntax tree of the formula.</returns>
        private Operation BuildAbstractSyntaxTree(string formulaText, ConstantRegistry compiledConstants)
        {
            TokenReader tokenReader = new TokenReader(cultureInfo);
            List<Token> tokens = tokenReader.Read(formulaText);
            
            AstBuilder astBuilder = new AstBuilder(FunctionRegistry, adjustVariableCaseEnabled, compiledConstants);
            Operation operation = astBuilder.Build(tokens);

            if (optimizerEnabled)
                return optimizer.Optimize(operation, this.FunctionRegistry, this.ConstantRegistry);
            else
                return operation;
        }

        private Func<IDictionary<string, double>, double> BuildFormula(string formulaText, ConstantRegistry compiledConstants, Operation operation)
        {
            return executionFormulaCache.GetOrAdd(GenerateFormulaCacheKey(formulaText, compiledConstants), v => executor.BuildFormula(operation, this.FunctionRegistry, this.ConstantRegistry));
        }

        private bool IsInFormulaCache(string formulaText, ConstantRegistry compiledConstants, out Func<IDictionary<string, double>, double> function)
        {
            function = null;
            return cacheEnabled && executionFormulaCache.TryGetValue(GenerateFormulaCacheKey(formulaText, compiledConstants), out function);
        }

        private string GenerateFormulaCacheKey(string formulaText, ConstantRegistry compiledConstants)
        {
            return (compiledConstants != null && compiledConstants.Any()) ? $"{formulaText}@{String.Join(",", compiledConstants?.Select(x => $"{x.ConstantName}:{x.Value}"))}" : formulaText;
        }

        /// <summary>
        /// Verify a collection of variables to ensure that all the variable names are valid.
        /// Users are not allowed to overwrite reserved variables or use function names as variables.
        /// If an invalid variable is detected an exception is thrown.
        /// </summary>
        /// <param name="variables">The colletion of variables that must be verified.</param>
        internal void VerifyVariableNames(IDictionary<string, double> variables)
        {
            foreach (string variableName in variables.Keys)
            {
                if(ConstantRegistry.IsConstantName(variableName) && !ConstantRegistry.GetConstantInfo(variableName).IsOverWritable)
                    throw new ArgumentException(string.Format("The name \"{0}\" is a reservered variable name that cannot be overwritten.", variableName), "variables");

                if (FunctionRegistry.IsFunctionName(variableName))
                    throw new ArgumentException(string.Format("The name \"{0}\" is a function name. Parameters cannot have this name.", variableName), "variables");
            }
        }
    }
}
