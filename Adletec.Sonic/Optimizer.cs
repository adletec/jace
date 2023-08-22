﻿using System.Collections.Generic;
using System.Linq;
using Adletec.Sonic.Execution;
using Adletec.Sonic.Operations;

namespace Adletec.Sonic
{
    public class Optimizer
    {
        private readonly IExecutor executor;

        public Optimizer(IExecutor executor)
        {
            this.executor = executor;
        }

        public Operation Optimize(Operation operation, IFunctionRegistry functionRegistry, IConstantRegistry constantRegistry)
        {
            if (!operation.DependsOnVariables && operation.IsIdempotent && operation.GetType() != typeof(IntegerConstant)
                && operation.GetType() != typeof(FloatingPointConstant))
            {
                double result = executor.Execute(operation, functionRegistry, constantRegistry);
                return new FloatingPointConstant(result);
            }

            if (operation.GetType() == typeof(Addition))
            {
                var addition = (Addition)operation;
                addition.Argument1 = Optimize(addition.Argument1, functionRegistry, constantRegistry);
                addition.Argument2 = Optimize(addition.Argument2, functionRegistry, constantRegistry);
            }
            else if (operation.GetType() == typeof(Subtraction))
            {
                var subtraction = (Subtraction)operation;
                subtraction.Argument1 = Optimize(subtraction.Argument1, functionRegistry, constantRegistry);
                subtraction.Argument2 = Optimize(subtraction.Argument2, functionRegistry, constantRegistry);
            }
            else if (operation.GetType() == typeof(Multiplication))
            {
                var multiplication = (Multiplication)operation;
                multiplication.Argument1 = Optimize(multiplication.Argument1, functionRegistry, constantRegistry);
                multiplication.Argument2 = Optimize(multiplication.Argument2, functionRegistry, constantRegistry);

                if ((multiplication.Argument1.GetType() == typeof(FloatingPointConstant) && ((FloatingPointConstant)multiplication.Argument1).Value == 0.0)
                    || (multiplication.Argument2.GetType() == typeof(FloatingPointConstant) && ((FloatingPointConstant)multiplication.Argument2).Value == 0.0))
                {
                    return new FloatingPointConstant(0.0);
                }
            }
            else if (operation.GetType() == typeof(Division))
            {
                var division = (Division)operation;
                division.Dividend = Optimize(division.Dividend, functionRegistry, constantRegistry);
                division.Divisor = Optimize(division.Divisor, functionRegistry, constantRegistry);
                if (division.Dividend.GetType() == typeof(FloatingPointConstant) && ((FloatingPointConstant)division.Dividend).Value == 0.0)
                {
                    return new FloatingPointConstant(0.0);
                }
            }
            else if (operation.GetType() == typeof(Exponentiation))
            {
                var exponentiation = (Exponentiation)operation;
                exponentiation.Base = Optimize(exponentiation.Base, functionRegistry, constantRegistry);
                exponentiation.Exponent = Optimize(exponentiation.Exponent, functionRegistry, constantRegistry);

                if (exponentiation.Exponent.GetType() == typeof(FloatingPointConstant) &&
                    ((FloatingPointConstant)exponentiation.Exponent).Value == 0.0)
                {
                    return new FloatingPointConstant(1.0);
                }
            }
            else if(operation.GetType() == typeof(Function))
            {
                var function = (Function)operation;
                IList<Operation> arguments = function.Arguments.Select(a => Optimize(a, functionRegistry, constantRegistry)).ToList();
                function.Arguments = arguments;
            }

            return operation;
        }
    }
}
