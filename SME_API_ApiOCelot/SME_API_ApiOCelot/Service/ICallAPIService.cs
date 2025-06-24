using SME_API_ApiOCelot.Models;

namespace SME_API_ApiOCelot.Services
{
    public interface ICallAPIService
    {
        Task RecErrorLogApiAsync(TErrorApiLogModels eModels, string strApikey);
    }
}
