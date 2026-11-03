namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupOrderArgumentTransformations
{
    [StepArgumentTransformation(Name = "group_requester", Regex = "(another customer|a guest|a waiter|an anonymous visitor)")]
    public static string GroupRequester(string value) => value;
}
