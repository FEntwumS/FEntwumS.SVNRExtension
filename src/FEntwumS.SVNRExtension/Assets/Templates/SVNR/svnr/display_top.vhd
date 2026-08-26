library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

entity display_top is
  port (
    reset         : in  std_ulogic;     -- low activ
    clk           : in  std_ulogic;
    address       : in  std_logic_vector(5 downto 0);
    data_to_ram   : in  std_logic_vector(15 downto 0);
    data_from_ram : out std_logic_vector(15 downto 0);
    writestrobe   : in  std_logic;
    ws2812_out    : out std_ulogic      -- Serieller Ausgang WS2812
    );
end display_top;

architecture behv of display_top is

  type mem_type is array (63 downto 0) of std_logic_vector(15 downto 0);
  signal mem : mem_type;

  component ram_to_bit is
    port (
      reset      : in  std_ulogic;
      clk        : in  std_ulogic;
      raddr      : out std_ulogic_vector(5 downto 0);
      dataRead   : in  std_ulogic_vector(15 downto 0);
      ws2812_out : out std_ulogic);
  end component ram_to_bit;

  signal raddr    : std_ulogic_vector(5 downto 0);
  signal dataRead : std_ulogic_vector(15 downto 0);


begin

  dpmem : process(clk)
  begin
    if rising_edge(clk) then
      if writestrobe = '1' then
        mem(to_integer(unsigned(address))) <= data_to_ram;
      end if;
    end if;
  end process dpmem;

  -- data_from_ram <= mem(to_integer(unsigned(address)));
  -- solange eine Schattenkopie im BRA ist, nicht nötig:
  data_from_ram <= x"5555";
  -- 2. Read - Port
  dataRead <= std_ulogic_vector(mem(to_integer(unsigned(std_logic_vector(raddr)))));

  ram_to_bit_1 : entity work.ram_to_bit
    port map (
      reset      => reset,
      clk        => clk,
      raddr      => raddr,
      dataRead   => dataRead,
      ws2812_out => ws2812_out);



end architecture behv;
