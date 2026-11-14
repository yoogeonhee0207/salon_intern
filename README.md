# 막내의 가위 (가제)

> 수건 접던 막내가 가위를 잡기까지, 인턴 한 명이 매장을 돌린다

번화가 프랜차이즈 미용실의 막내 인턴이 되어, 하루하루 일을 배우고 디자이너로 성장하는 타이쿤 + 미니게임 + 스토리 혼합 게임입니다.

## 현재 상태

기획 단계 (기획서 v2.1)

## 플랫폼

모바일 (가로 화면), Unity (C#), 1인 개발, 무료 + 광고 + 인앱결제

## 핵심 콘셉트

- 하루 루프: 아침 준비(예약 확인) → 실시간 영업(틈 휴식) → 저녁 마감 → 야간 연습
- 레벨 Lv.0~6: 레벨이 안 되면 해당 시술은 할 수 없음
- 샴푸, 롤 말기, 염색, 드라이, 가발 커트, 모델 커트 순으로 미니게임 해금
- Lv.4 부인턴장, Lv.5 인턴장이 되어 인턴 휴식과 업무를 배분
- 새 게임마다 랜덤으로 뽑히는 인턴 동료
- 인턴 편 엔딩을 보면 디자이너 과정 해금

## 폴더 구조

```
salon-intern-game/
├── README.md
├── docs/
│   ├── design/
│   │   └── game-design.md        # 기획서 (최신)
│   └── changelog.md              # 기획 변경 이력
├── characters/                   # 고정 인물 설정
└── game/                         # Unity 게임 프로젝트 (game/SalonIntern)
```

## 문서

- [기획서](docs/design/game-design.md)
- [변경 이력](docs/changelog.md)

## 고정 인물

- [원장 한서린](characters/01-director-han-seorin.md)
- [수석 디자이너 최하준](characters/02-chief-choi-hajun.md)
- [커트 디자이너 박태양](characters/03-cut-park-taeyang.md)
- [다정 디자이너 송몽실](characters/04-kind-song-mongsil.md)
- [신입 디자이너 조은새](characters/05-junior-jo-eunsae.md)

이름은 가명이며 언제든 바꿀 수 있다.

## 미결정 사항

- 나머지 디자이너 7명의 성격 (랜덤 시스템 적용 여부)
- 개발 일정과 단계별 목표
