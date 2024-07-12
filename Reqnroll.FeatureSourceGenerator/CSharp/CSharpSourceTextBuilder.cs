﻿using System.Collections.Immutable;
using System.Text;
using Reqnroll.FeatureSourceGenerator.SourceModel;

namespace Reqnroll.FeatureSourceGenerator.CSharp;

public class CSharpSourceTextBuilder
{
    private static readonly UTF8Encoding Encoding = new(false);

    private readonly StringBuilder _buffer = new();

    private bool _isFreshLine = true;

    private const string Indent = "    ";

    private CodeBlock _context = new();

    /// <summary>
    /// Gets the depth of the current block.
    /// </summary>
    public int Depth => _context.Depth;

    public CSharpSourceTextBuilder Append(char c)
    {
        AppendIndentIfIsFreshLine();

        _buffer.Append(c);
        return this;
    }

    public CSharpSourceTextBuilder Append(string text)
    {
        AppendIndentIfIsFreshLine();

        _buffer.Append(text);
        return this;
    }

    public CSharpSourceTextBuilder AppendLiteralList<T>(IEnumerable<T> values)
    {
        var first = true;

        foreach (var value in values)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                Append(", ");
            }

            AppendLiteral(value);
        }

        return this;
    }

    public CSharpSourceTextBuilder AppendLiteral(object? value)
    {
        return value switch
        {
            null => Append("null"),
            string s => AppendLiteral(s),
            ImmutableArray<string> array => AppendLiteralArray(array),
            ImmutableArray<object?> array => AppendLiteralArray(array),
            _ => throw new NotSupportedException($"Values of type {value.GetType().FullName} cannot be encoded as a literal value in C#.")
        };
    }

    public CSharpSourceTextBuilder AppendLiteralArray<T>(ImmutableArray<T> array)
    {
        if (!CSharpSyntax.TypeAliases.TryGetValue(typeof(T), out var typeName))
        {
            typeName = $"global::{typeof(T).FullName}";
        }

        Append("new ").Append(typeName);

        if (array.Length == 0) 
        {
            return Append("[0]");
        }

        return Append("[] { ").AppendLiteralList(array).Append(" }");
    }

    public CSharpSourceTextBuilder AppendLiteral(string? s)
    {
        if (s == null)
        {
            return Append("null");
        }

        var escapedValue = CSharpSyntax.FormatLiteral(s);

        Append(escapedValue);
        return this;
    }

    private void AppendIndentIfIsFreshLine()
    {
        if (_isFreshLine)
        {
            AppendIndentToDepth();

            _isFreshLine = false;
        }
    }

    private void AppendIndentToDepth()
    {
        for (var i = 0; i < Depth; i++)
        {
            _buffer.Append(Indent);
        }
    }

    internal void Reset() => _buffer.Clear();

    public CSharpSourceTextBuilder AppendDirective(string directive)
    {
        if (!_isFreshLine)
        {
            throw new InvalidOperationException(CSharpSourceTextBuilderResources.CannotAppendDirectiveUnlessAtStartOfLine);
        }

        _buffer.Append(directive);

        _buffer.AppendLine();

        _isFreshLine = true;
        return this;
    }

    public CSharpSourceTextBuilder AppendLine()
    {
        AppendIndentIfIsFreshLine();

        _buffer.AppendLine();

        _isFreshLine = true;
        return this;
    }

    public CSharpSourceTextBuilder AppendLine(string text)
    {
        AppendIndentIfIsFreshLine();

        _buffer.AppendLine(text);

        _isFreshLine = true;
        return this;
    }

    /// <summary>
    /// Starts a new code block.
    /// </summary>
    public CSharpSourceTextBuilder BeginBlock()
    {
        _context = new CodeBlock(_context);
        return this;
    }

    /// <summary>
    /// Appends the specified text and starts a new block.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public CSharpSourceTextBuilder BeginBlock(string text) => AppendLine(text).BeginBlock();

    /// <summary>
    /// Ends the current block and begins a new line.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <para>The builder is not currently in a block (the <see cref="Depth"/> is zero.)</para>
    /// </exception>
    public CSharpSourceTextBuilder EndBlock()
    {
        if (_context.Parent == null)
        {
            throw new InvalidOperationException(CSharpSourceTextBuilderResources.NotInCodeBlock);
        }

        _context = _context.Parent;
        return AppendLine();
    }

    /// <summary>
    /// Ends the current block, appends the specified text and begins a new line.
    /// </summary>
    /// <param name="text">The text to append.</param>
    /// <exception cref="InvalidOperationException">
    /// <para>The builder is not currently in a block (the <see cref="Depth"/> is zero.)</para>
    /// </exception>
    public CSharpSourceTextBuilder EndBlock(string text)
    {
        if (_context.Parent == null)
        {
            throw new InvalidOperationException(CSharpSourceTextBuilderResources.NotInCodeBlock);
        }

        _context = _context.Parent;
        return AppendLine(text);
    }

    /// <summary>
    /// Gets the value of this instance as a string.
    /// </summary>
    /// <returns>A string containing all text appended to the builder.</returns>
    public override string ToString() => _buffer.ToString();

    private class CodeBlock
    {
        public CodeBlock()
        {
            Depth = 0;
        }

        public CodeBlock(CodeBlock parent)
        {
            Parent = parent;
            Depth = parent.Depth + 1;
        }

        public int Depth { get; }

        public CodeBlock? Parent { get; }

        public bool InSection { get; set; }

        public bool HasSection { get; set; }
    }

    public SourceText ToSourceText()
    {
        return SourceText.From(_buffer.ToString(), Encoding);
    }

    public CSharpSourceTextBuilder AppendAttributeBlock(AttributeDescriptor attribute)
    {
        Append('[');
        AppendTypeReference(attribute.Type);

        if (attribute.NamedArguments.Count > 0 || attribute.PositionalArguments.Length > 0)
        {
            Append('(');

            AppendLiteralList(attribute.PositionalArguments);

            var firstProperty = true;
            foreach (var (name, value) in attribute.NamedArguments)
            {
                if (firstProperty)
                {
                    if (!attribute.PositionalArguments.IsEmpty)
                    {
                        Append(", ");
                    }

                    firstProperty = false;
                }
                else
                {
                    Append(", ");
                }

                Append(name).Append(" = ").AppendLiteral(value);
            }

            Append(')');
        }

        Append(']');

        return this;
    }

    public CSharpSourceTextBuilder AppendTypeReference(TypeIdentifier type)
    {
        return type switch
        {
            LocalTypeIdentifier localType => AppendTypeReference(localType),
            QualifiedTypeIdentifier qualifiedType => AppendTypeReference(qualifiedType),
            ArrayTypeIdentifier arrayType => AppendTypeReference(arrayType),
            NestedTypeIdentifier nestedType => AppendTypeReference(nestedType),
            _ => throw new NotImplementedException($"Appending references of type {type.GetType().Name} is not implemented.")
        };
    }

    public CSharpSourceTextBuilder AppendTypeReference(LocalTypeIdentifier type)
    {
        return type switch
        {
            SimpleTypeIdentifier simpleType => AppendTypeReference(simpleType),
            GenericTypeIdentifier genericType => AppendTypeReference(genericType),
            _ => throw new NotImplementedException($"Appending references of type {type.GetType().Name} is not implemented.")
        };
    }

    public CSharpSourceTextBuilder AppendTypeReference(NestedTypeIdentifier type)
    {
        return AppendTypeReference(type.EncapsulatingType).Append('.').AppendTypeReference(type.LocalType);
    }

    public CSharpSourceTextBuilder AppendTypeReference(SimpleTypeIdentifier type)
    {
        Append(type.Name);

        if (type.IsNullable)
        {
            Append('?');
        }

        return this;
    }

    public CSharpSourceTextBuilder AppendTypeReference(GenericTypeIdentifier type)
    {
        Append(type.Name);

        Append('<');

        AppendTypeReference(type.TypeArguments[0]);

        for (var i = 1;  i < type.TypeArguments.Length; i++)
        {
            Append(',');
            AppendTypeReference(type.TypeArguments[i]);
        }

        Append('>');

        if (type.IsNullable)
        {
            Append('?');
        }

        return this;
    }

    public CSharpSourceTextBuilder AppendTypeReference(QualifiedTypeIdentifier type)
    {
        Append("global::").Append(type.Namespace).Append('.');

        AppendTypeReference(type.LocalType);

        return this;
    }

    public CSharpSourceTextBuilder AppendTypeReference(ArrayTypeIdentifier type)
    {
        AppendTypeReference(type.ItemType).Append("[]");

        if (type.IsNullable)
        {
            Append('?');
        }

        return this;
    }
}
