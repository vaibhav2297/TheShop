using FluentAssertions;
using FluentValidation;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using Xunit;

namespace TheShop.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_InvalidRequest_PreservesEveryFieldAndLegacyError(bool typedResult)
    {
        var validator = new InlineValidator<TestRequest>();
        validator.RuleFor(x => x.Email).NotEmpty().WithMessage("Email_Required")
            .WithState(_ => (IReadOnlyList<string>)["existing-detail"]);
        validator.RuleFor(x => x.Name).NotEmpty().WithMessage("Name_Required");
        var called = false;
        var request = new TestRequest("", "");

        Result result;
        if (typedResult)
        {
            var behavior = new ValidationBehavior<TestRequest, Result<int>>([validator]);
            result = await behavior.Handle(request, _ =>
            {
                called = true;
                return Task.FromResult(Result.Ok(1));
            }, TestContext.Current.CancellationToken);
        }
        else
        {
            var behavior = new ValidationBehavior<TestRequest, Result>([validator]);
            result = await behavior.Handle(request, _ =>
            {
                called = true;
                return Task.FromResult(Result.Ok());
            }, TestContext.Current.CancellationToken);
        }

        called.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Email_Required");
        result.ErrorArgs.Should().Equal("existing-detail");
        result.ValidationErrors.Should().Equal(
            new FieldValidationError(nameof(TestRequest.Email), "Email_Required"),
            new FieldValidationError(nameof(TestRequest.Name), "Name_Required"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ValidRequest_InvokesHandlerOnceAndPreservesItsResult(bool withValidator)
    {
        var validator = new InlineValidator<TestRequest>();
        validator.RuleFor(x => x.Email).NotEmpty();
        var behavior = new ValidationBehavior<TestRequest, Result<int>>(withValidator ? [validator] : []);
        var expected = Result.Fail<int>("BusinessFailure", ["detail"]);
        var calls = 0;

        var result = await behavior.Handle(new TestRequest("user@example.com", "Name"), token =>
        {
            token.Should().Be(TestContext.Current.CancellationToken);
            calls++;
            return Task.FromResult(expected);
        }, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        result.ValidationErrors.Should().BeEmpty();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AsyncRule_AwaitsValidationOnceAndPassesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var validator = new InlineValidator<TestRequest>();
        var calls = 0;
        validator.RuleFor(x => x.Email).MustAsync(async (_, token) =>
        {
            await Task.Yield();
            token.Should().Be(cancellation.Token);
            calls++;
            return false;
        }).WithMessage("Email_Invalid");
        var behavior = new ValidationBehavior<TestRequest, Result>([validator]);

        var result = await behavior.Handle(new TestRequest("invalid", "Name"),
            _ => throw new InvalidOperationException("Handler must not run."), cancellation.Token);

        calls.Should().Be(1);
        result.ValidationErrors.Should().ContainSingle()
            .Which.Should().Be(new FieldValidationError(nameof(TestRequest.Email), "Email_Invalid"));
    }

    [Fact]
    public async Task Handle_InvalidNonResultResponse_PreservesValidationException()
    {
        var validator = new InlineValidator<TestRequest>();
        validator.RuleFor(x => x.Email).NotEmpty().WithMessage("Email_Required");
        var behavior = new ValidationBehavior<TestRequest, string>([validator]);

        var act = () => behavior.Handle(new TestRequest("", "Name"),
            _ => throw new InvalidOperationException("Handler must not run."), TestContext.Current.CancellationToken);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(TestRequest.Email));
    }

    [Fact]
    public void Fail_ValidationDetails_AreASnapshotWhileExistingFactoriesHaveNoDetails()
    {
        var errors = new List<FieldValidationError> { new("Email", "Email_Invalid") };
        var result = Result.Fail<int>("Email_Invalid", [], errors);
        errors.Clear();

        result.ValidationErrors.Should().ContainSingle();
        Result.Ok().ValidationErrors.Should().BeEmpty();
        Result.Ok(1).ValidationErrors.Should().BeEmpty();
        Result.Fail("BusinessFailure").ValidationErrors.Should().BeEmpty();
        Result.Fail<int>("BusinessFailure").ValidationErrors.Should().BeEmpty();
    }

    private sealed record TestRequest(string Email, string Name);
}
