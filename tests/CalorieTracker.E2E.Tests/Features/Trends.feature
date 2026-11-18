Feature: Trends
  Trends shows how a week, a month or a year went. It opens from the bar on the current week; its figures are covered by bUnit and Core's unit tests (the emulator's clock and diary are not fixed), so here the screen is driven through the period control, the arrows and the calendar, whatever the diary holds.

  @smoke
  Scenario: Trends opens from the bar on the current week
    Given I am on the Dashboard
    When I open Trends from the bar
    Then I am on Trends showing the current week
    And Trends is the current item in the bar
    And the navigation bar shows Today, Add and Trends only

  Scenario: The period control switches between week, month and year
    Given I am on the Dashboard
    And I have opened Trends
    When I choose Month on Trends
    Then Trends shows the current month
    When I choose Year on Trends
    Then Trends shows the current year
    When I choose Week on Trends
    Then Trends shows the current week

  Scenario: The previous arrow moves back a week, and next stops at the current week
    Given I am on the Dashboard
    And I have opened Trends
    Then next is not available on Trends
    When I go back a week on Trends
    Then Trends shows the week before the current one
    And next is available on Trends
    When I go forward a week on Trends
    Then Trends shows the current week
    And next is not available on Trends

  Scenario: Tapping a day on the calendar opens that day on Today
    Given I am on the Dashboard
    And I go back a day on the Dashboard
    And I have added 100 g of the first food found by "oat" to Lunch
    And the snackbar goes away
    And I have opened Trends
    When I choose Month on Trends
    And I show the month of yesterday on Trends
    And I tap yesterday on the Trends calendar
    Then I am on the Dashboard on yesterday

  Scenario: Android back from Trends goes to Today
    Given I am on the Dashboard
    And I have opened Trends
    When I press Android back
    Then I am on the Dashboard

  Scenario: The Add sheet opens on Trends and Search foods adds to a meal
    Given I am on the Dashboard
    And I have opened Trends
    When I open the Add sheet on Trends
    And I tap Search foods on the Add sheet
    Then I am on search to add to a meal
