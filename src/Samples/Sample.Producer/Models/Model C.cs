using Twino.Client.TMQ.Annotations;

namespace Sample.Producer.Models
{
    [ContentType(5)]
    [DirectTarget(FindTargetBy.Name, "consumer")]
    public class ModelC
    {
        public string Value { get; set; }
    }
}