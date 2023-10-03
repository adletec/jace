﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Adletec.Sonic.Tokenizer
{
    /// <summary>
    /// A token reader that converts the input string in a list of tokens.
    /// </summary>
    public class TokenReader
    {
        private readonly CultureInfo cultureInfo;
        private readonly char decimalSeparator;
        private readonly char argumentSeparator;

        public TokenReader() 
            : this(CultureInfo.CurrentCulture)
        {
        }

        public TokenReader(CultureInfo cultureInfo)
        {
            this.cultureInfo = cultureInfo;
            this.decimalSeparator = cultureInfo.NumberFormat.NumberDecimalSeparator[0];
            this.argumentSeparator = cultureInfo.TextInfo.ListSeparator[0];
        }

        /// <summary>
        /// Read in the provided formula and convert it into a list of tokens that can be processed by the
        /// Abstract Syntax Tree Builder.
        /// </summary>
        /// <param name="formula">The formula that must be converted into a list of tokens.</param>
        /// <returns>The list of tokens for the provided formula.</returns>
        public List<Token> Read(string formula)
        {
            if (string.IsNullOrEmpty(formula))
                throw new ArgumentNullException(nameof(formula));

            var tokens = new List<Token>();

            var characters = formula.ToCharArray();

            var isFormulaSubPart = true;
            var isScientific = false;

            for(var i = 0; i < characters.Length; i++)
            {
                if (IsPartOfNumeric(characters[i], true, false, isFormulaSubPart))
                {
                    var buffer = new StringBuilder();
                    buffer.Append(characters[i]);
                    var startPosition = i;
                                       

                    while (++i < characters.Length && IsPartOfNumeric(characters[i], false, characters[i-1] == '-', isFormulaSubPart))
                    {
                        if (isScientific && IsScientificNotation(characters[i]))
                            throw new InvalidTokenParserException($"Invalid token \"{characters[i]}\" detected at position {i}.", i, 1, characters[i].ToString());

                        if (IsScientificNotation(characters[i]))
                        {
                            isScientific = IsScientificNotation(characters[i]);

                            if (characters.Length > i + 1 && characters[i + 1] == '-')
                            {
                                buffer.Append(characters[i++]);
                            }
                        }

                        buffer.Append(characters[i]);
                    }

                    // Verify if we do not have an int
                    if (int.TryParse(buffer.ToString(), out var intValue))
                    {
                        tokens.Add(new Token { TokenType = TokenType.Integer, Value = intValue, StartPosition = startPosition, Length = i - startPosition });
                        isFormulaSubPart = false;
                    }
                    else
                    {
                        if (buffer.ToString() == "-")
                        {
                            // Verify if we have a unary minus, we use the token '_' for a unary minus in the AST builder
                            tokens.Add(new Token { TokenType = TokenType.Operation, Value = '_', StartPosition = startPosition, Length = 1 });
                        }
                        else if (double.TryParse(buffer.ToString(), NumberStyles.Float | NumberStyles.AllowThousands,
                            cultureInfo, out var doubleValue))
                        {
                            tokens.Add(new Token { TokenType = TokenType.FloatingPoint, Value = doubleValue, StartPosition = startPosition, Length = i - startPosition });
                            isScientific = false;
                            isFormulaSubPart = false;
                        }
                        else
                        {
                            throw new InvalidFloatingPointNumberException($"Invalid floating point number: {buffer}",
                                startPosition, i - startPosition, buffer.ToString());
                        }
                    }

                    if (i == characters.Length)
                    {
                        // Last character read
                        continue;
                    }
                }

                if (IsPartOfVariable(characters[i], true))
                {
                    var buffer = "" + characters[i];
                    var startPosition = i;

                    while (++i < characters.Length && IsPartOfVariable(characters[i], false))
                    {
                        buffer += characters[i];
                    }

                    tokens.Add(new Token { TokenType = TokenType.Text, Value = buffer, StartPosition = startPosition, Length = i -startPosition });
                    isFormulaSubPart = false;

                    if (i == characters.Length)
                    {
                        // Last character read
                        continue;
                    }
                }
                if (characters[i] == this.argumentSeparator)
                {
                    tokens.Add(new Token { TokenType = TokenType.ArgumentSeparator, Value = characters[i], StartPosition = i, Length = 1 });
                    isFormulaSubPart = false;
                }
                else
                {
                    switch (characters[i])
                    { 
                        case ' ':
                            continue;
                        case '+':
                        case '-':
                        case '*':
                        case '/':
                        case '^':
                        case '%':
                        case '≤':
                        case '≥':
                        case '≠':
                            if (IsUnaryMinus(characters[i], tokens))
                            {
                                // We use the token '_' for a unary minus in the AST builder
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '_', StartPosition = i, Length = 1 });
                            }
                            else
                            {
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = characters[i], StartPosition = i, Length = 1 });                            
                            }
                            isFormulaSubPart = true;
                            break;
                        case '(':
                            tokens.Add(new Token { TokenType = TokenType.LeftBracket, Value = characters[i], StartPosition = i, Length = 1 });
                            isFormulaSubPart = true;
                            break;
                        case ')':
                            tokens.Add(new Token { TokenType = TokenType.RightBracket, Value = characters[i], StartPosition = i, Length = 1 });
                            isFormulaSubPart = false;
                            break;
                        case '<':
                            if (i + 1 < characters.Length && characters[i + 1] == '=')
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '≤', StartPosition = i++, Length = 2 });
                            else
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '<', StartPosition = i, Length = 1 });
                            isFormulaSubPart = false;
                            break;
                        case '>':
                            if (i + 1 < characters.Length && characters[i + 1] == '=')
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '≥', StartPosition = i++, Length = 2 });
                            else
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '>', StartPosition = i, Length = 1 });
                            isFormulaSubPart = false;
                            break;
                        case '!':
                            if (i + 1 < characters.Length && characters[i + 1] == '=')
                            {
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '≠', StartPosition = i++, Length = 2 });
                                isFormulaSubPart = false;
                            }
                            else
                                throw new InvalidTokenParserException($"Invalid token \"{characters[i]}\" detected at position {i}.", i, 1, characters[i].ToString());
                            break;
                        case '&':
                            if (i + 1 < characters.Length && characters[i + 1] == '&')
                            {
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '&', StartPosition = i++, Length = 2 });
                                isFormulaSubPart = false;
                            }
                            else
                                throw new InvalidTokenParserException($"Invalid token \"{characters[i]}\" detected at position {i}.", i, 1, characters[i].ToString());
                            break;
                        case '|':
                            if (i + 1 < characters.Length && characters[i + 1] == '|')
                            {
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '|', StartPosition = i++, Length = 2 });
                                isFormulaSubPart = false;
                            }
                            else
                                throw new InvalidTokenParserException($"Invalid token \"{characters[i]}\" detected at position {i}.", i, 1, characters[i].ToString());
                            break;
                        case '=':
                            if (i + 1 < characters.Length && characters[i + 1] == '=')
                            {
                                tokens.Add(new Token { TokenType = TokenType.Operation, Value = '=', StartPosition = i++, Length = 2 });
                                isFormulaSubPart = false;
                            }
                            else
                                throw new InvalidTokenParserException($"Invalid token \"{characters[i]}\" detected at position {i}.", i, 1, characters[i].ToString());
                            break;
                        default:
                            throw new InvalidTokenParserException($"Invalid token \"{characters[i]}\" detected at position {i}.", i, 1, characters[i].ToString());
                    }
                }
            }

            return tokens;
        }

        private bool IsPartOfNumeric(char character, bool isFirstCharacter, bool afterMinus, bool isFormulaSubPart)
        {
            return character == decimalSeparator || (character >= '0' && character <= '9') || (isFormulaSubPart && isFirstCharacter && character == '-') || (!isFirstCharacter && !afterMinus && character == 'e') || (!isFirstCharacter && character == 'E');
        }

        private bool IsPartOfVariable(char character, bool isFirstCharacter)
        {
            return (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') || (!isFirstCharacter && character >= '0' && character <= '9') || (!isFirstCharacter && character == '_');
        }

        private bool IsUnaryMinus(char currentToken, IList<Token> tokens)
        {
            if (currentToken != '-') return false;
            var previousToken = tokens[tokens.Count - 1];

            return !(previousToken.TokenType == TokenType.FloatingPoint ||
                     previousToken.TokenType == TokenType.Integer ||
                     previousToken.TokenType == TokenType.Text ||
                     previousToken.TokenType == TokenType.RightBracket);

        }

        private bool IsScientificNotation(char currentToken)
        {
            return currentToken == 'e' || currentToken == 'E';
        }
    }
}
