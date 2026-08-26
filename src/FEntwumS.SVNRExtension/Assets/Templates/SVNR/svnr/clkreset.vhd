library IEEE;
use IEEE.STD_LOGIC_1164.all;
use ieee.numeric_std.all;

entity clkreset is
  port (
    clk_input_pin    : in  std_logic;
    mem_init_running : in  std_logic;
    clk_t            : out std_logic;
    reset_init       : out std_logic;
    reset_mem        : out std_logic
    );
end clkreset;

architecture Behavioral of clkreset is
  signal external_rstn : std_logic;
  signal syncreset : std_logic_vector(2 downto 0);
begin

  process(clk_input_pin) is
    variable cnt : unsigned(7 downto 0) := (others => '0');
  begin
    if rising_edge(clk_input_pin) then
      if cnt < 255 then
        cnt := cnt + 1;
        external_rstn <= '1';
      else
        external_rstn <= '0';
      end if;
    end if;
  end process;


  -- Speicher bekommt kurzen, gefilteteren reset
  reset_mem <= external_rstn;

  -- Rest des SVNRwird in reset gehalten, solange der Speicher noch
  -- initialisert wird
  reset_init <= mem_init_running or external_rstn;

  -- Oder die PLL
  clk_t <= clk_input_pin;



end Behavioral;
