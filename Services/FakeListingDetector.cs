using System;
using System.Collections.Generic;
using System.Linq;
using OnHouseLocal.Models;

namespace OnHouseLocal.Services
{
    public class FakeListingDetector
    {
        /// <summary>
        /// 매물 목록에 허위/의심 신호 및 손님 매칭 정보를 지능형으로 주입
        /// </summary>
        public void AnalyzeDeals(List<DanggeunDealItem> deals, List<CustomerRequestItem> customers)
        {
            if (deals == null || deals.Count == 0) return;

            // 1. 작성자별 등록 빈도 계산
            var authorCounts = deals
                .Where(d => !string.IsNullOrWhiteSpace(d.AuthorName) && d.AuthorName != "집주인/세입자" && d.AuthorName != "당근 이웃/집주인")
                .GroupBy(d => d.AuthorName)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var deal in deals)
            {
                var signals = new List<string>();

                // (1) 동일 작성자 다수 등록 체크
                if (!string.IsNullOrWhiteSpace(deal.AuthorName) && authorCounts.TryGetValue(deal.AuthorName, out int cnt) && cnt >= 4)
                {
                    signals.Add($"⚠️ 동일 작성자 {cnt}건 등록");
                }

                // (2) 비정상적인 초저가 시세 의심 (원룸/오피스텔 월세 15만원 이하 등)
                if (deal.MonthlyRent > 0 && deal.MonthlyRent <= 15 && deal.Deposit <= 500)
                {
                    signals.Add("⚠️ 시세 대비 초저가(미끼 의심)");
                }
                else if (deal.Deposit > 0 && deal.Deposit <= 1000 && deal.MonthlyRent == 0 && !deal.PriceDisplay.Contains("단기"))
                {
                    // 전세 1000만원 이하 (서울 비현실적 전세가)
                    signals.Add("⚠️ 비현실적 전세가(확인필요)");
                }

                // (3) 키워드 의심 신호
                string text = $"{deal.Title} {deal.Description}";
                if (text.Contains("카톡 아이디") || text.Contains("카카오톡 아이디") || text.Contains("선입금") || text.Contains("계약금 먼저"))
                {
                    signals.Add("🚨 사기/선입금 주의");
                }

                deal.SuspiciousSignal = string.Join(" · ", signals);

                // (4) 손님 조건 자동 매칭
                if (customers != null && customers.Count > 0)
                {
                    var matchedList = new List<string>();
                    foreach (var c in customers.Where(c => c.IsActive))
                    {
                        bool regionMatch = string.IsNullOrWhiteSpace(c.TargetRegion) || 
                                           c.TargetRegion.Split(',', ' ').Any(r => !string.IsNullOrWhiteSpace(r) && (deal.Region.Contains(r) || deal.Title.Contains(r)));

                        bool depositMatch = deal.Deposit <= c.MaxDeposit;
                        bool rentMatch = c.TransactionType == "전세" ? deal.MonthlyRent == 0 : (deal.MonthlyRent <= c.MaxMonthlyRent);

                        if (regionMatch && depositMatch && rentMatch)
                        {
                            matchedList.Add(c.CustomerName);
                        }
                    }

                    if (matchedList.Count > 0)
                    {
                        deal.MatchedCustomerInfo = $"🙋 {string.Join(", ", matchedList)} 손님 일치";
                    }
                }
            }
        }

        /// <summary>
        /// 내 장부 등록 매물에 대해 손님 매칭 분석
        /// </summary>
        public void MatchCustomersForProperties(List<PropertyItem> properties, List<CustomerRequestItem> customers)
        {
            if (properties == null || customers == null || customers.Count == 0) return;

            foreach (var prop in properties)
            {
                var matched = new List<string>();
                foreach (var c in customers.Where(c => c.IsActive))
                {
                    bool typeMatch = c.PropertyType == "전체" || prop.PropertyType.Contains(c.PropertyType);
                    bool transMatch = c.TransactionType == "전체" || prop.TransactionType == c.TransactionType;
                    bool regionMatch = string.IsNullOrWhiteSpace(c.TargetRegion) ||
                                       c.TargetRegion.Split(',', ' ').Any(r => !string.IsNullOrWhiteSpace(r) && (prop.Address.Contains(r) || prop.Title.Contains(r)));
                    bool depositMatch = prop.Deposit >= c.MinDeposit && prop.Deposit <= c.MaxDeposit;
                    bool rentMatch = c.TransactionType == "전세" ? prop.MonthlyRent == 0 : (prop.MonthlyRent <= c.MaxMonthlyRent);

                    if (typeMatch && transMatch && regionMatch && depositMatch && rentMatch)
                    {
                        matched.Add(c.CustomerName);
                    }
                }

                if (matched.Count > 0)
                {
                    prop.MatchedCustomerInfo = $"🙋 {string.Join(", ", matched)} 손님 조건 일치";
                }
            }
        }
    }
}
