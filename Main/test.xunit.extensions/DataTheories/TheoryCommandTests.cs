using System;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Extensions;
using Xunit.Sdk;

public class TheoryCommandTests
{
    [Fact]
    public void ExecuteCreatesClassAndRunsTest()
    {
        MethodInfo methodInfo = typeof(InstrumentedSpy).GetMethod("PassedTest");
        TheoryCommand command = new TheoryCommand(Reflector.Wrap(methodInfo), null);
        InstrumentedSpy.ctorCounter = 0;
        InstrumentedSpy.passedTestCounter = 0;

        command.Execute(new InstrumentedSpy());

        Assert.Equal(1, InstrumentedSpy.ctorCounter);
        Assert.Equal(1, InstrumentedSpy.passedTestCounter);
    }

    [Fact]
    public void ExecuteStubTestFixtureVerifyBeforeAfterTestCalledOnce()
    {
        MethodInfo methodInfo = typeof(DisposableSpy).GetMethod("PassedTest");
        TheoryCommand command = new TheoryCommand(Reflector.Wrap(methodInfo), null);
        DisposableSpy.ctorCalled = 0;
        DisposableSpy.disposeCalled = 0;

        ITestResult result = command.Execute(new DisposableSpy());

        Assert.IsType<PassedResult>(result);
    }

    [Fact]
    public void NotEnoughData()
    {
        TheoryCommand command = new TheoryCommand(Reflector.Wrap(typeof(ParameterSpy).GetMethod("Method")),
                                                  new object[] { 2 });

        Assert.Throws<InvalidOperationException>(() => command.Execute(new ParameterSpy()));
    }

    [Fact]
    public void UsesDisplayName()
    {
        MethodInfo methodInfo = typeof(DummyWithAttributes).GetMethod("TheoryMethod");

        TheoryCommand command = new TheoryCommand(Reflector.Wrap(methodInfo), new object[] { 42, 24.5 });

        Assert.Equal("My display name(x: 42, y: 24.5)", command.DisplayName);
    }

    [Fact]
    public void DisplayNameWithTooManyValues()
    {
        MethodInfo methodInfo = typeof(DummyWithAttributes).GetMethod("TheoryMethod");

        TheoryCommand command = new TheoryCommand(Reflector.Wrap(methodInfo), new object[] { 42, 24.5, "Hello!" });

        Assert.Equal("My display name(x: 42, y: 24.5, ???: \"Hello!\")", command.DisplayName);
    }

    [Fact]
    public void DisplayNameWithTooFewValues()
    {
        MethodInfo methodInfo = typeof(DummyWithAttributes).GetMethod("TheoryMethod");

        TheoryCommand command = new TheoryCommand(Reflector.Wrap(methodInfo), new object[] { 42 });

        Assert.Equal("My display name(x: 42, y: ???)", command.DisplayName);
    }

    [Fact]
    public void PassesParametersToTest()
    {
        MethodInfo methodInfo = typeof(SpyWithDataPassed).GetMethod("Test");
        TheoryCommand command = new TheoryCommand(Reflector.Wrap(methodInfo), new object[] { 42, 24.5, "foo" });
        SpyWithDataPassed.X = 0;
        SpyWithDataPassed.Y = 0.0;
        SpyWithDataPassed.Z = null;

        command.Execute(new SpyWithDataPassed());

        Assert.Equal(42, SpyWithDataPassed.X);
        Assert.Equal(24.5, SpyWithDataPassed.Y);
        Assert.Equal("foo", SpyWithDataPassed.Z);
    }

    [Fact]
    public void TestMethodReturnPassedResult()
    {
        MethodInfo methodInfo = typeof(TestMethodCommandClass).GetMethod("TestMethod");
        TheoryCommand command = new TheoryCommand(Reflector.Wrap(methodInfo), null);

        MethodResult result = command.Execute(new TestMethodCommandClass());

        Assert.IsType<PassedResult>(result);
    }

    [Fact]
    public void ThrowsExceptionReturnFailedResult()
    {
        MethodInfo methodInfo = typeof(TestMethodCommandClass).GetMethod("ThrowsException");
        TheoryCommand command = new TheoryCommand(Reflector.Wrap(methodInfo), null);

        Assert.Throws<InvalidOperationException>(() => command.Execute(new TestMethodCommandClass()));
    }

    [Fact]
    public void TooMuchData()
    {
        TheoryCommand command = new TheoryCommand(Reflector.Wrap(typeof(ParameterSpy).GetMethod("Method")),
                                                  new object[] { 2, "foo", 3.14 });

        Assert.Throws<InvalidOperationException>(() => command.Execute(new ParameterSpy()));
    }

    [Fact]
    public void TruncatesVeryLongStrings()
    {
        StringBuilder sb = new StringBuilder(500);

        for (int idx = 0; idx < 50; idx++)
            sb.Append("----=----|");

        TheoryCommand command = new TheoryCommand(Reflector.Wrap(typeof(ParameterSpy).GetMethod("Method")),
                                                  new object[] { 2, sb.ToString() });

        MethodResult result = command.Execute(new ParameterSpy());

        Assert.IsType<PassedResult>(result);
        Assert.Equal(@"TheoryCommandTests+ParameterSpy.Method(x: 2, y: ""----=----|----=----|----=----|----=----|----=----|""...)", result.DisplayName);
    }

    [Fact]
    public void SettingTheoryTimeoutSetsTimeout()
    {
        TheoryCommand command = new TheoryCommand(Reflector.Wrap(typeof(DummyWithAttributes).GetMethod("TimeoutMethod")), null);

        Assert.Equal(153, command.Timeout);
    }

    [Fact]
    public void StringDataWithEmbeddedNullCreatesValidXml()  // CodePlex issue #9755, &#x0 is not valid XML, so we replace it with \0
    {
        string expectedDisplayName = @"TheoryCommandTests+DummyWithAttributes.StringMethod(s: ""\0"")";
        string expectedXml = @"<start name=""TheoryCommandTests+DummyWithAttributes.StringMethod(s: &quot;\0&quot;)"" type=""TheoryCommandTests+DummyWithAttributes"" method=""StringMethod"" />";

        TheoryCommand command = new TheoryCommand(Reflector.Wrap(typeof(DummyWithAttributes).GetMethod("StringMethod")), new object[] { "\0" });

        Assert.Equal(expectedDisplayName, command.DisplayName);
        Assert.Equal(expectedXml, command.ToStartXml().OuterXml);
    }

    internal class DummyWithAttributes
    {
        [Theory(DisplayName = "My display name")]
        public void TheoryMethod(int x, double y) { }

        [Theory(Timeout = 153)]
        public void TimeoutMethod() { }

        [Theory]
        public void StringMethod(string s) { }
    }

    internal class DisposableSpy : IDisposable
    {
        public static int ctorCalled;
        public static int disposeCalled;

        public DisposableSpy()
        {
            ctorCalled++;
        }

        public void Dispose()
        {
            disposeCalled++;
        }

        public void PassedTest() { }
    }

    internal class DisposableSpyWithConstructorThrow : IDisposable
    {
        public static int ctorCalled;
        public static int disposeCalled;
        public static int testCalled;

        public DisposableSpyWithConstructorThrow()
        {
            ctorCalled++;
            throw new InvalidOperationException("Constructor Failed");
        }

        public void Dispose()
        {
            disposeCalled++;
        }

        public void PassedTest()
        {
            testCalled++;
        }
    }

    internal class DisposableSpyWithDisposeThrow : IDisposable
    {
        public static int ctorCalled;
        public static int disposeCalled;
        public static int testCalled;

        public DisposableSpyWithDisposeThrow()
        {
            ctorCalled++;
        }

        public void Dispose()
        {
            disposeCalled++;
            throw new InvalidOperationException("Dispose Failed");
        }

        public void PassedTest()
        {
            testCalled++;
        }
    }

    internal class DisposableSpyWithTestThrow : IDisposable
    {
        public static int ctorCalled;
        public static int disposeCalled;
        public static int testCalled;

        public DisposableSpyWithTestThrow()
        {
            ctorCalled++;
        }

        public void Dispose()
        {
            disposeCalled++;
        }

        public void FailedTest()
        {
            testCalled++;
            throw new InvalidOperationException("Dispose Failed");
        }
    }

    internal class InstrumentedSpy
    {
        public static int ctorCounter;
        public static int passedTestCounter;

        public InstrumentedSpy()
        {
            ctorCounter++;
        }

        public void PassedTest()
        {
            passedTestCounter++;
        }
    }

    internal class ParameterSpy
    {
        public void Method(int x, string y)
        {
        }
    }

    internal class SpyWithDataPassed
    {
        public static int X;
        public static double Y;
        public static string Z;

        public void Test(int x, double y, string z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    internal class TestMethodCommandClass
    {
        public void TestMethod() { }

        public void ThrowsException()
        {
            throw new InvalidOperationException();
        }
    }
}