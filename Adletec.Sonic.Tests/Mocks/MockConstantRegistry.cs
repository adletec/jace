﻿using System;
using System.Collections;
using System.Collections.Generic;
using Adletec.Sonic.Execution;

namespace Adletec.Sonic.Tests.Mocks;

public class MockConstantRegistry : IConstantRegistry
{
    private readonly HashSet<string> constantNames;

    public MockConstantRegistry()
        : this(new[] { "e", "pi" })
    {
    }

    public MockConstantRegistry(IEnumerable<string> constantNames)
    {
        this.constantNames = new HashSet<string>(constantNames);
    }

    public ConstantInfo GetConstantInfo(string constantName)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<ConstantInfo> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public bool IsConstantName(string constantName)
    {
        throw new NotImplementedException();
    }

    public void RegisterConstant(string constantName, double value)
    {
        throw new NotImplementedException();
    }

    public void RegisterConstant(string constantName, double value, bool isOverWritable)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}