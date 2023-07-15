# MY FIRST MASTODON BOT

- 실행하면 마스토돈 인스턴스의 정해진 계정에 날씨를 올립니다.
- 단기예보와 기상시황을 가져와 적절하게 가공합니다.
  - 시간별 기준 위치가 하드코딩되어 있음, 설정으로 빼야 합니다.
- [공공데이터포털](https://www.data.go.kr/index.do) 단기예보, 기상시황 오픈API를 이용합니다.

## 옵션

```
-v : verbose, 각종 일반 로그를 같이 출력합니다.
-l : local, path/test 내의 sample.json, sample2.json을 대신 이용합니다.
-n : notoot, 실제로 toot하는 대신 화면에 출력만 합니다.
```

## 테스트 파일

- 실행파일 경로에 `test` 폴더를 추가하고, `sample.json`과 `sample2.json`을 추가해 로컬 데이터를 가져오는 것이 가능합니다.
  - 네, 대충 만든 것 같습니다.

## 설정파일 포맷

- 실행파일과 동일한 위치에 `settings.toml`이라는 TOML 파일을 추가해야 합니다.

```toml
serviceKey = "공공데이터포털에서_발급받은_디코딩된_인증키"
instance = "사용할_인스턴스_주소"
accessToken = "어플리케이션_Access_Token"
maxRetryCount = 5
delay = 2.5
```

## 라즈베리 파이용으로 올리는 방법

- 자세한 사항은 [이 링크](https://learn.microsoft.com/en-us/dotnet/iot/deployment#deploying-a-self-contained-app)를 참조하세요.
- systemd-timer나 crontab에 등록시켜 돌리는 것을 추천합니다.

```bash
dotnet publish --runtime linux-arm64 --self-contained

scp -r /publish/* pi@raspberrypi:/home/pi/deployment-location/

ssh pi@raspberrypi

$ cd /home/pi/deployment-location/
$ chmod +x mastodon_bot
$ ./mastodon_bot # 실행
```

