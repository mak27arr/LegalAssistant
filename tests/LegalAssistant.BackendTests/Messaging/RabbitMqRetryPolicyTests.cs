using System.Text;
using LegalAssistant.Messaging;

namespace LegalAssistant.BackendTests.Messaging;

public sealed class RabbitMqRetryPolicyTests
{
    [Fact]
    public void GetAttempts_ShouldReadCommonRabbitHeaderRepresentations()
    {
        Assert.Equal(2, RabbitMqRetryPolicy.GetAttempts(new Dictionary<string, object>
        {
            [RabbitMqRetryPolicy.AttemptsHeader] = Encoding.UTF8.GetBytes("2")
        }));
        Assert.Equal(3, RabbitMqRetryPolicy.GetAttempts(new Dictionary<string, object>
        {
            [RabbitMqRetryPolicy.AttemptsHeader] = "3"
        }));
        Assert.Equal(4, RabbitMqRetryPolicy.GetAttempts(new Dictionary<string, object>
        {
            [RabbitMqRetryPolicy.AttemptsHeader] = 4L
        }));
    }

    [Fact]
    public void NextDelaySeconds_ShouldUseExponentialBackoffAndCap()
    {
        var options = new RabbitMqProcessingOptions
        {
            InitialDelaySeconds = 5,
            MaxDelaySeconds = 20,
            BackoffMultiplier = 2
        };

        Assert.Equal(5, RabbitMqRetryPolicy.NextDelaySeconds(1, options));
        Assert.Equal(10, RabbitMqRetryPolicy.NextDelaySeconds(2, options));
        Assert.Equal(20, RabbitMqRetryPolicy.NextDelaySeconds(3, options));
        Assert.Equal(20, RabbitMqRetryPolicy.NextDelaySeconds(5, options));
    }

    [Fact]
    public void SetAttempts_ShouldWriteAttemptHeader()
    {
        var headers = new Dictionary<string, object>();

        RabbitMqRetryPolicy.SetAttempts(headers, 3);

        Assert.Equal(3, headers[RabbitMqRetryPolicy.AttemptsHeader]);
    }
}
