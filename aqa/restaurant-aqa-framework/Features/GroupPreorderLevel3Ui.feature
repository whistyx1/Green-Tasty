@ui @level3 @level3ui
Feature: Owner removes a guest before sending the restaurant order

  Background:
    Given an isolated group-order restaurant is ready for level three
    And the Owner has booked a table through the storefront for level three

  Rule: Removed selections do not reach the restaurant

    Scenario: Owner removes a confirmed guest and the Waiter sees only the remaining order
      Given guests have selected confirmed and draft carts in the storefront
      Then the Owner sees the confirmed guest before removal
      When the Owner removes that confirmed guest in the storefront
      Then the removed confirmed guest cannot start another pre-order
      Then the Owner sees the remaining guests and recalculated total
      When the Owner sends the remaining group pre-order in the storefront
      Then the Owner sees only the remaining confirmed dishes in the sent order
      When the assigned Waiter opens the reduced reservation pre-order
      Then the Waiter sees only the remaining confirmed dishes

    Scenario: Removed choosing guest loses the open menu without a reload
      Given guests have selected confirmed and draft carts in the storefront
      When the Owner removes the choosing guest in the storefront
      Then the removed guest sees that the pre-order is inactive
      And the removed guest stays inactive after reloading
      And the Owner does not see the removed guest after reloading
