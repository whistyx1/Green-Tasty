@ui @level2 @level2ui
Feature: The Owner sends one group order to the restaurant

  Background:
    Given an isolated group-order restaurant is ready for level two
    And the Owner has booked a table through the storefront for level two

  Rule: The restaurant receives only the confirmed selections

    Scenario: Owner confirms the group order and the Waiter sees the same dishes
      Given the Owner has a saved dish and guests have confirmed and draft carts in the storefront
      When the Owner submits the consolidated group pre-order in the storefront
      Then the Owner sees the sent status and exact confirmed quantities
      When the assigned Waiter opens the reservation pre-order in the storefront
      Then the Waiter sees the same confirmed dishes and quantities
