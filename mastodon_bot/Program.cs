using System.Text.Json;

namespace mastodon_bot
{
    class Program
    {
        static void Main(string[] args)
        {
            var provider = new Provider();
            var serviceKey = provider.GetServiceKey();
            if (serviceKey == null)
            {
                throw new Exception("서비스키를 불러올 수 없습니다.");
                return;
            }

            var position = provider.GetPositionBasedOnTime(DateTime.Now);

            var weatherFetcher = new WeatherFetcher(serviceKey, position);
            var reportFetcher = new WeatherReportFetcher(serviceKey);

            // TODO 비동기 프로그래밍을 제대로 이용하기

            var weatherContent = weatherFetcher.FetchAsync().Result;
            var reportContent = reportFetcher.FetchAsync().Result;

            // TODO JSON 파싱하기
            // JSON 객체 상태에서 작업할지, C# 객체 상태에서 작업할지 결정하기

            // 각 Item은 baseData, baseTime, category, fcstDate, fcstTime, fcstValue, nx, ny를 가지고 있음
            // baseData, baseTime : 일기예보를 한 날짜와 시간
            // nx, ny : 예보 지점의 x, y 좌표 (보낸 것과 동일할 것이다.)

            // fcstData, fcstTime : 예보의 대상이 되는 날짜와 시간
            // category : 예보 항목, fcstValue : 예보 값
            // 예보 항목은 POP(강수확률), PTY(강수형태), R06(6시간 강수량), REH(습도), S06(6시간 신적설), SKY(하늘상태), T3H(3시간 기온), TMN(아침 최저기온), TMX(낮 최고기온), UUU(풍속(동서성분)), VVV(풍속(남북성분)), WAV(파고), VEC(풍향), WSD(풍속)가 있음
            // 각 항목의 단위는 각각 %, 코드값, 벡터, %, 벡터, 코드값, ℃, ℃, ℃, m/s, m/s, M, deg, m/s
            // 코드값은 별도로 Dict를 만드는 것이 좋겠다.

            // (fcstData, fcstTime)별로 묶은 자료구조 하나 (여러 (category, fcstValue)를 가질 것이다.)
            // category별로 묶은 자료구조 하나 (여러 (fcstData, fcstTime, fcstValue)를 가질 것이다.)
            // private 생성자로 넣고, human readable한 프로퍼티로 접근하자.

            //Console.WriteLine(weatherContent);
            //Console.WriteLine(reportContent); // body.items.item[0].wfSv1 을 출력하면 됨
        }
    }
}