namespace Shared.Api.Localization;

public interface IErrorLocalizer
{
	string Localize(string errorCode, string culture);
}
